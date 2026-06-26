using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ModelContextProtocol.Extensions.Tasks;

/// <summary>
/// Provides extension methods that add MCP Tasks extension (SEP-2663) support to an MCP server.
/// </summary>
/// <remarks>
/// The Tasks extension lets a server offload long-running tool executions to the background and lets a
/// client poll for completion via <c>tasks/get</c>, supply input via <c>tasks/update</c>, and cancel via
/// <c>tasks/cancel</c>. State is held in an <see cref="IMcpTaskStore"/>.
/// </remarks>
public static class McpServerTasksExtensions
{
    /// <summary>
    /// Enables the MCP Tasks extension on the supplied <paramref name="options"/> using the given task store.
    /// </summary>
    /// <param name="options">The server options to configure.</param>
    /// <param name="store">The task store that backs task state.</param>
    /// <returns>The <paramref name="options"/> instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> or <paramref name="store"/> is <see langword="null"/>.</exception>
    public static McpServerOptions WithTasks(this McpServerOptions options, IMcpTaskStore store)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (store is null) throw new ArgumentNullException(nameof(store));

        // Advertise the tasks extension in server capabilities.
        options.Capabilities ??= new ServerCapabilities();
        options.Capabilities.Extensions ??= new Dictionary<string, object>();
        options.Capabilities.Extensions[McpExtensions.Tasks] = new JsonObject();

        options.RawRequestHandlerConfigurators ??= new List<Action<IMcpServerRawHandlerRegistry>>();
        options.RawRequestHandlerConfigurators.Add(registry => ConfigureTaskHandlers(registry, store));

        return options;
    }

    /// <summary>
    /// Enables the MCP Tasks extension on the server built by <paramref name="builder"/> using the given task store.
    /// </summary>
    /// <param name="builder">The server builder.</param>
    /// <param name="store">The task store that backs task state.</param>
    /// <returns>The <paramref name="builder"/> instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="store"/> is <see langword="null"/>.</exception>
    public static IMcpServerBuilder WithTasks(this IMcpServerBuilder builder, IMcpTaskStore store)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (store is null) throw new ArgumentNullException(nameof(store));

        builder.Services.AddSingleton<IPostConfigureOptions<McpServerOptions>>(
            new TasksPostConfigureOptions(store));
        return builder;
    }

    /// <summary>
    /// Sends a task status notification (<c>notifications/tasks/status</c>) to the connected client.
    /// </summary>
    /// <param name="server">The server sending the notification.</param>
    /// <param name="notificationParams">The task status notification parameters to send.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="server"/> or <paramref name="notificationParams"/> is <see langword="null"/>.</exception>
    public static Task SendTaskStatusNotificationAsync(
        this McpServer server,
        TaskStatusNotificationParams notificationParams,
        CancellationToken cancellationToken = default)
    {
        if (server is null) throw new ArgumentNullException(nameof(server));
        if (notificationParams is null) throw new ArgumentNullException(nameof(notificationParams));

        var paramsNode = JsonSerializer.SerializeToNode(notificationParams, TasksJsonContext.Default.TaskStatusNotificationParams);
        return server.SendNotificationAsync(
            TaskMethods.StatusNotification,
            paramsNode,
            McpJsonUtilities.DefaultOptions,
            cancellationToken);
    }

    private sealed class TasksPostConfigureOptions(IMcpTaskStore store) : IPostConfigureOptions<McpServerOptions>
    {
        public void PostConfigure(string? name, McpServerOptions options) => options.WithTasks(store);
    }

    private static void ConfigureTaskHandlers(IMcpServerRawHandlerRegistry registry, IMcpTaskStore store)
    {
        // Per-task cancellation sources shared between the tools/call task wrapper and the tasks/cancel handler.
        var cancellationSources = new ConcurrentDictionary<string, CancellationTokenSource>();

        registry.SetHandler(TaskMethods.Get, async (request, cancellationToken) =>
        {
            EnsureDraft(registry, request, TaskMethods.Get);

            var requestParams = Deserialize(request.Params, TasksJsonContext.Default.GetTaskRequestParams);
            var info = await store.GetTaskAsync(requestParams.TaskId, cancellationToken).ConfigureAwait(false);
            if (info is null)
            {
                throw new McpProtocolException($"Unknown task: '{requestParams.TaskId}'", McpErrorCode.InvalidParams);
            }

            return JsonSerializer.SerializeToNode(ToGetTaskResult(info), TasksJsonContext.Default.GetTaskResult);
        });

        registry.SetHandler(TaskMethods.Update, async (request, cancellationToken) =>
        {
            EnsureDraft(registry, request, TaskMethods.Update);

            var requestParams = Deserialize(request.Params, TasksJsonContext.Default.UpdateTaskRequestParams);

            // RequestParams deserializes inputResponses via an internal backing property that this assembly's
            // source-generated context cannot access, so read the inputResponses node explicitly.
            IDictionary<string, InputResponse> inputResponses = new Dictionary<string, InputResponse>();
            if (request.Params is JsonObject paramsObject &&
                paramsObject.TryGetPropertyValue("inputResponses", out var inputResponsesNode) &&
                inputResponsesNode is not null)
            {
                inputResponses = JsonSerializer.Deserialize(inputResponsesNode, TasksJsonContext.Default.IDictionaryStringInputResponse)
                    ?? inputResponses;
            }

            await store.ResolveInputRequestsAsync(requestParams.TaskId, inputResponses, cancellationToken).ConfigureAwait(false);

            return JsonSerializer.SerializeToNode(new UpdateTaskResult(), TasksJsonContext.Default.UpdateTaskResult);
        });

        registry.SetHandler(TaskMethods.Cancel, async (request, cancellationToken) =>
        {
            EnsureDraft(registry, request, TaskMethods.Cancel);

            var requestParams = Deserialize(request.Params, TasksJsonContext.Default.CancelTaskRequestParams);

            // Idempotent ack per SEP-2663: always return CancelTaskResult regardless of whether the task
            // was known/cancellable. The store's SetCancelledAsync no-ops for unknown or terminal tasks.
            await store.SetCancelledAsync(requestParams.TaskId, cancellationToken).ConfigureAwait(false);

            // Signal the task's CancellationTokenSource if one exists. Whichever side (this handler or the
            // background runner's finally block) wins TryRemove owns disposal.
            if (cancellationSources.TryRemove(requestParams.TaskId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }

            return JsonSerializer.SerializeToNode(new CancelTaskResult(), TasksJsonContext.Default.CancelTaskResult);
        });

        // Wrap tools/call so that when the client opts in (and the session negotiated draft), the tool
        // execution is offloaded to the background via the store.
        registry.TryWrapHandler(RequestMethods.ToolsCall, inner => async (request, cancellationToken) =>
        {
            var callToolParams = request.Params is null
                ? null
                : JsonSerializer.Deserialize(request.Params, TasksJsonContext.Default.CallToolRequestParams);

            // The SEP-2663 Tasks extension is draft-only. Only materialize a task when the request was
            // negotiated under the draft revision AND the client opted in; otherwise run the inner handler
            // and return the direct result.
            if (registry.IsDraftProtocolRequest(request) && HasTaskExtensionOptIn(callToolParams?.Meta))
            {
                var taskInfo = await store.CreateTaskAsync(cancellationToken).ConfigureAwait(false);
                var taskId = taskInfo.TaskId;

                var cts = new CancellationTokenSource();
                cancellationSources[taskId] = cts;

                // Capture the token synchronously before Task.Run dispatches the work. The cancel handler
                // may race with the background runner: whichever side wins TryRemove owns disposal.
                var taskCancellationToken = cts.Token;

                _ = Task.Run(async () =>
                {
                    var previousInterceptor = McpServer.CurrentOutgoingRequestInterceptor;
                    McpServer.CurrentOutgoingRequestInterceptor = BuildTaskInterceptor(store, taskId);
                    try
                    {
                        var resultNode = await inner(request, taskCancellationToken).ConfigureAwait(false);
                        var resultElement = ToElement(resultNode);
                        await store.SetCompletedAsync(taskId, resultElement).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (taskCancellationToken.IsCancellationRequested)
                    {
                        await store.SetCancelledAsync(taskId, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (InputRequiredException)
                    {
                        // MRTR (input requests) cannot be composed with the task-store wrapper for
                        // [McpServerTool] methods today: the task ID was already returned synchronously,
                        // so we have no way to surface InputRequiredResult to the client retroactively.
                        var error = new JsonRpcErrorDetail
                        {
                            Code = (int)McpErrorCode.InvalidRequest,
                            Message = "MRTR (input requests) and tasks cannot be composed via [McpServerTool] yet; " +
                                      "drive the input-request loop manually from within the task body.",
                        };
                        await store.SetFailedAsync(taskId, ToElement(error, TasksJsonContext.Default.JsonRpcErrorDetail)).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // SEP-2663 186: failed.error MUST be a JSON-RPC error object {code, message, data?}.
                        // McpProtocolException carries a JSON-RPC ErrorCode and is documented as safe to
                        // propagate. For any other exception type, redact the message and use InternalError.
                        var error = ex is McpProtocolException mcpEx
                            ? new JsonRpcErrorDetail { Code = (int)mcpEx.ErrorCode, Message = mcpEx.Message }
                            : new JsonRpcErrorDetail { Code = (int)McpErrorCode.InternalError, Message = "An error occurred while executing the task." };
                        await store.SetFailedAsync(taskId, ToElement(error, TasksJsonContext.Default.JsonRpcErrorDetail)).ConfigureAwait(false);
                    }
                    finally
                    {
                        McpServer.CurrentOutgoingRequestInterceptor = previousInterceptor;

                        // Only the side that wins TryRemove disposes cts, preventing a double-dispose race
                        // with the default tasks/cancel handler.
                        if (cancellationSources.TryRemove(taskId, out var registeredCts))
                        {
                            registeredCts.Dispose();
                        }
                    }
                }, CancellationToken.None);

                return JsonSerializer.SerializeToNode(ToCreateTaskResult(taskInfo), TasksJsonContext.Default.CreateTaskResult);
            }

            return await inner(request, cancellationToken).ConfigureAwait(false);
        });
    }

    private static McpOutgoingRequestInterceptor BuildTaskInterceptor(IMcpTaskStore store, string taskId) =>
        async (method, paramsNode, cancellationToken) =>
        {
            var requestId = Guid.NewGuid().ToString("N");
            JsonElement? paramsElement = paramsNode is null
                ? null
                : JsonSerializer.SerializeToElement(paramsNode, TasksJsonContext.Default.JsonNode);

            var inputRequest = new InputRequest
            {
                Method = method,
                Params = paramsElement,
            };

            var tcs = new TaskCompletionSource<InputResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Handler(InputResponseReceivedEventArgs args)
            {
                if (args.TaskId == taskId && args.RequestId == requestId)
                {
                    tcs.TrySetResult(args.Response);
                }
            }

            store.InputResponseReceived += Handler;
            try
            {
                await store.SetInputRequestsAsync(
                    taskId,
                    new Dictionary<string, InputRequest> { [requestId] = inputRequest },
                    cancellationToken).ConfigureAwait(false);

#if NET
                var response = await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
#else
                InputResponse response;
                using (cancellationToken.Register(static s => ((TaskCompletionSource<InputResponse>)s!).TrySetCanceled(), tcs))
                {
                    response = await tcs.Task.ConfigureAwait(false);
                }
#endif

                return response.RawValue.ValueKind == JsonValueKind.Undefined
                    ? null
                    : JsonNode.Parse(response.RawValue.GetRawText());
            }
            finally
            {
                store.InputResponseReceived -= Handler;
            }
        };

    private static bool HasTaskExtensionOptIn(JsonObject? meta) =>
        meta is not null &&
        meta[MetaKeys.ClientCapabilities] is JsonObject caps &&
        caps["extensions"] is JsonObject exts &&
        exts.ContainsKey(McpExtensions.Tasks);

    private static void EnsureDraft(IMcpServerRawHandlerRegistry registry, JsonRpcRequest request, string method)
    {
        // The tasks/* methods do not exist before the draft revision (SEP-2663). Reject them with
        // MethodNotFound when the request was negotiated under a legacy protocol version.
        if (!registry.IsDraftProtocolRequest(request))
        {
            throw new McpProtocolException(
                $"The method '{method}' requires the draft protocol revision.",
                McpErrorCode.MethodNotFound);
        }
    }

    private static T Deserialize<T>(JsonNode? node, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo) =>
        (node is null ? default : JsonSerializer.Deserialize(node, typeInfo))
            ?? throw new McpProtocolException("Invalid or missing request parameters.", McpErrorCode.InvalidParams);

    private static JsonElement ToElement(JsonNode? node) =>
        node is null
            ? default
            : JsonSerializer.SerializeToElement(node, TasksJsonContext.Default.JsonNode);

    private static JsonElement ToElement<T>(T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo) =>
        JsonSerializer.SerializeToElement(value, typeInfo);

    internal static CreateTaskResult ToCreateTaskResult(McpTaskInfo info) => new()
    {
        TaskId = info.TaskId,
        Status = info.Status,
        CreatedAt = info.CreatedAt,
        LastUpdatedAt = info.LastUpdatedAt,
        TimeToLive = info.TimeToLive,
        PollIntervalMs = info.PollIntervalMs,
        StatusMessage = info.StatusMessage,
        ResultType = "task",
    };

    internal static GetTaskResult ToGetTaskResult(McpTaskInfo info) => info.Status switch
    {
        McpTaskStatus.Working => new WorkingTaskResult
        {
            TaskId = info.TaskId,
            CreatedAt = info.CreatedAt,
            LastUpdatedAt = info.LastUpdatedAt,
            TimeToLive = info.TimeToLive,
            PollIntervalMs = info.PollIntervalMs,
            StatusMessage = info.StatusMessage,
            ResultType = "complete",
        },
        McpTaskStatus.Completed => new CompletedTaskResult
        {
            TaskId = info.TaskId,
            CreatedAt = info.CreatedAt,
            LastUpdatedAt = info.LastUpdatedAt,
            TimeToLive = info.TimeToLive,
            PollIntervalMs = info.PollIntervalMs,
            StatusMessage = info.StatusMessage,
            Result = info.Result ?? throw new InvalidOperationException($"Task '{info.TaskId}' is completed but has no result."),
            ResultType = "complete",
        },
        McpTaskStatus.Failed => new FailedTaskResult
        {
            TaskId = info.TaskId,
            CreatedAt = info.CreatedAt,
            LastUpdatedAt = info.LastUpdatedAt,
            TimeToLive = info.TimeToLive,
            PollIntervalMs = info.PollIntervalMs,
            StatusMessage = info.StatusMessage,
            Error = info.Error ?? throw new InvalidOperationException($"Task '{info.TaskId}' is failed but has no error."),
            ResultType = "complete",
        },
        McpTaskStatus.Cancelled => new CancelledTaskResult
        {
            TaskId = info.TaskId,
            CreatedAt = info.CreatedAt,
            LastUpdatedAt = info.LastUpdatedAt,
            TimeToLive = info.TimeToLive,
            PollIntervalMs = info.PollIntervalMs,
            StatusMessage = info.StatusMessage,
            ResultType = "complete",
        },
        McpTaskStatus.InputRequired => new InputRequiredTaskResult
        {
            TaskId = info.TaskId,
            CreatedAt = info.CreatedAt,
            LastUpdatedAt = info.LastUpdatedAt,
            TimeToLive = info.TimeToLive,
            PollIntervalMs = info.PollIntervalMs,
            StatusMessage = info.StatusMessage,
            InputRequests = info.InputRequests is IDictionary<string, InputRequest> dict
                ? dict
                : info.InputRequests?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    ?? new Dictionary<string, InputRequest>(),
            ResultType = "complete",
        },
        _ => throw new InvalidOperationException($"Unknown task status: {info.Status}"),
    };
}
