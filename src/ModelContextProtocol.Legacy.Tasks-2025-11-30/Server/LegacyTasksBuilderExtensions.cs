using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ModelContextProtocol.Legacy.Tasks;

/// <summary>Provides server-builder extensions for the 2025-11-30 Tasks draft.</summary>
public static class LegacyTasksBuilderExtensions
{
    /// <summary>
    /// Enables the legacy Tasks protocol for task-augmented <c>tools/call</c> requests.
    /// </summary>
    /// <remarks>
    /// The registered server can also use the 2026 Tasks extension. Each implementation only
    /// handles requests for its own negotiated protocol revision.
    /// </remarks>
    public static IMcpServerBuilder WithLegacyTasks(this IMcpServerBuilder builder, ILegacyMcpTaskStore store)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (store is null)
        {
            throw new ArgumentNullException(nameof(store));
        }

        builder.Services.AddSingleton<IPostConfigureOptions<McpServerOptions>>(
            new LegacyTasksPostConfigureOptions(store));
        return builder;
    }

    private sealed class LegacyTasksPostConfigureOptions(ILegacyMcpTaskStore store) : IPostConfigureOptions<McpServerOptions>
    {
        private readonly ILegacyMcpTaskStore _store = store;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationSources = new(StringComparer.Ordinal);

        public void PostConfigure(string? name, McpServerOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            options.Capabilities ??= new ServerCapabilities();
            options.Capabilities.AdditionalProperties ??= new Dictionary<string, JsonElement>();
            options.Capabilities.AdditionalProperties[LegacyTasksProtocol.CapabilityName] = CreateCapabilityElement();

            if (options.ToolCollection is not null)
            {
                foreach (var tool in options.ToolCollection)
                {
                    tool.ProtocolTool.AdditionalProperties ??= new Dictionary<string, JsonElement>();
                    tool.ProtocolTool.AdditionalProperties["execution"] = CreateToolExecutionElement();
                }
            }

            options.RequestHandlers ??= new List<McpServerRequestHandler>();
            options.RequestHandlers.Add(new McpServerRequestHandler
            {
                Method = LegacyTasksProtocol.GetTaskMethod,
                IsApplicable = IsLegacyTasksProtocolRequest,
                Handler = HandleGetTaskAsync,
            });
            options.RequestHandlers.Add(new McpServerRequestHandler
            {
                Method = LegacyTasksProtocol.ListTasksMethod,
                IsApplicable = IsLegacyTasksProtocolRequest,
                Handler = HandleListTasksAsync,
            });
            options.RequestHandlers.Add(new McpServerRequestHandler
            {
                Method = LegacyTasksProtocol.GetTaskResultMethod,
                IsApplicable = IsLegacyTasksProtocolRequest,
                Handler = HandleGetTaskResultAsync,
            });
            options.RequestHandlers.Add(new McpServerRequestHandler
            {
                Method = LegacyTasksProtocol.CancelTaskMethod,
                IsApplicable = IsLegacyTasksProtocolRequest,
                Handler = HandleCancelTaskAsync,
            });

            options.Filters.Request.CallToolWithAlternateFilters.Add(next => async (request, cancellationToken) =>
            {
                if (!IsLegacyTasksRequest(request))
                {
                    return await next(request, cancellationToken).ConfigureAwait(false);
                }

                var metadata = request.JsonRpcRequest.Params?[LegacyTasksProtocol.TaskPropertyName]?
                    .Deserialize(LegacyTasksJsonContext.Default.McpLegacyTaskMetadata)
                    ?? new McpLegacyTaskMetadata();
                var task = await _store.CreateTaskAsync(metadata, cancellationToken).ConfigureAwait(false);
                var taskCancellation = new CancellationTokenSource();
                _cancellationSources[task.TaskId] = taskCancellation;

                _ = Task.Run(
                    () => ExecuteTaskAsync(task.TaskId, request, next, taskCancellation),
                    CancellationToken.None);

                return ResultOrAlternate<CallToolResult>.FromAlternate(
                    new CreateLegacyTaskResult { Task = task },
                    LegacyTasksJsonContext.Default.CreateLegacyTaskResult);
            });
        }

        private async Task ExecuteTaskAsync(
            string taskId,
            RequestContext<CallToolRequestParams> request,
            McpRequestHandler<CallToolRequestParams, ResultOrAlternate<CallToolResult>> next,
            CancellationTokenSource taskCancellation)
        {
            try
            {
                var execution = await next(request, taskCancellation.Token).ConfigureAwait(false);
                if (execution.IsAlternate is true)
                {
                    throw new InvalidOperationException(
                        $"The legacy Tasks compatibility package cannot compose with another alternate result for task '{taskId}'.");
                }

                var result = execution.Result!;
                await _store.StoreTaskResultAsync(
                    taskId,
                    result.IsError is true ? McpLegacyTaskStatus.Failed : McpLegacyTaskStatus.Completed,
                    JsonSerializer.SerializeToElement(result, McpJsonUtilities.DefaultOptions.GetTypeInfo<CallToolResult>()),
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (taskCancellation.IsCancellationRequested)
            {
                await _store.CancelTaskAsync(taskId, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                var error = new CallToolResult
                {
                    IsError = true,
                    Content = [new TextContentBlock { Text = "An error occurred while executing the legacy task." }],
                };

                await _store.StoreTaskResultAsync(
                    taskId,
                    McpLegacyTaskStatus.Failed,
                    JsonSerializer.SerializeToElement(error, McpJsonUtilities.DefaultOptions.GetTypeInfo<CallToolResult>()),
                    CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                if (_cancellationSources.TryRemove(taskId, out var registered))
                {
                    registered.Dispose();
                }
            }
        }

        private async ValueTask<JsonNode?> HandleGetTaskAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            GateToLegacyProtocol(request, LegacyTasksProtocol.GetTaskMethod);
            var parameters = request.Params?.Deserialize(LegacyTasksJsonContext.Default.GetLegacyTaskRequestParams)
                ?? throw new McpProtocolException("Missing params for tasks/get.", McpErrorCode.InvalidParams);
            var task = await _store.GetTaskAsync(parameters.TaskId, cancellationToken).ConfigureAwait(false)
                ?? throw new McpProtocolException($"Unknown task '{parameters.TaskId}'.", McpErrorCode.InvalidParams);

            return JsonSerializer.SerializeToNode(ToGetTaskResult(task), LegacyTasksJsonContext.Default.GetLegacyTaskResult);
        }

        private async ValueTask<JsonNode?> HandleListTasksAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            GateToLegacyProtocol(request, LegacyTasksProtocol.ListTasksMethod);
            var parameters = request.Params?.Deserialize(LegacyTasksJsonContext.Default.ListLegacyTasksRequestParams)
                ?? new ListLegacyTasksRequestParams();
            var result = await _store.ListTasksAsync(parameters.Cursor, cancellationToken).ConfigureAwait(false);

            return JsonSerializer.SerializeToNode(result, LegacyTasksJsonContext.Default.ListLegacyTasksResult);
        }

        private async ValueTask<JsonNode?> HandleGetTaskResultAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            GateToLegacyProtocol(request, LegacyTasksProtocol.GetTaskResultMethod);
            var parameters = request.Params?.Deserialize(LegacyTasksJsonContext.Default.GetLegacyTaskPayloadRequestParams)
                ?? throw new McpProtocolException("Missing params for tasks/result.", McpErrorCode.InvalidParams);

            try
            {
                var result = await _store.GetTaskResultAsync(parameters.TaskId, cancellationToken).ConfigureAwait(false);
                return JsonNode.Parse(result.GetRawText());
            }
            catch (InvalidOperationException exception)
            {
                throw new McpProtocolException(exception.Message, McpErrorCode.InvalidParams);
            }
        }

        private async ValueTask<JsonNode?> HandleCancelTaskAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            GateToLegacyProtocol(request, LegacyTasksProtocol.CancelTaskMethod);
            var parameters = request.Params?.Deserialize(LegacyTasksJsonContext.Default.CancelLegacyTaskRequestParams)
                ?? throw new McpProtocolException("Missing params for tasks/cancel.", McpErrorCode.InvalidParams);
            var task = await _store.CancelTaskAsync(parameters.TaskId, cancellationToken).ConfigureAwait(false);

            if (_cancellationSources.TryRemove(task.TaskId, out var taskCancellation))
            {
                taskCancellation.Cancel();
                taskCancellation.Dispose();
            }

            return JsonSerializer.SerializeToNode(ToCancelTaskResult(task), LegacyTasksJsonContext.Default.CancelLegacyTaskResult);
        }

        private static bool IsLegacyTasksRequest(RequestContext<CallToolRequestParams> request) =>
            string.Equals(request.Server.NegotiatedProtocolVersion, LegacyTasksProtocol.ProtocolVersion, StringComparison.Ordinal) &&
            request.JsonRpcRequest.Params?[LegacyTasksProtocol.TaskPropertyName] is not null;

        private static bool IsLegacyTasksProtocolRequest(JsonRpcRequest request) =>
            string.Equals(request.Context?.ProtocolVersion, LegacyTasksProtocol.ProtocolVersion, StringComparison.Ordinal);

        private static void GateToLegacyProtocol(JsonRpcRequest request, string method)
        {
            if (!IsLegacyTasksProtocolRequest(request))
            {
                throw new McpProtocolException(
                    $"The method '{method}' requires protocol version '{LegacyTasksProtocol.ProtocolVersion}'.",
                    McpErrorCode.MethodNotFound);
            }
        }

        private static JsonElement CreateCapabilityElement() =>
            JsonSerializer.SerializeToElement(
                new JsonObject
                {
                    ["list"] = new JsonObject(),
                    ["cancel"] = new JsonObject(),
                    ["requests"] = new JsonObject
                    {
                        ["tools"] = new JsonObject
                        {
                            ["call"] = new JsonObject(),
                        },
                    },
                },
                McpJsonUtilities.DefaultOptions.GetTypeInfo<JsonNode>());

        private static JsonElement CreateToolExecutionElement() =>
            JsonSerializer.SerializeToElement(
                new JsonObject { ["taskSupport"] = "optional" },
                McpJsonUtilities.DefaultOptions.GetTypeInfo<JsonNode>());

        private static GetLegacyTaskResult ToGetTaskResult(McpLegacyTask task) =>
            new()
            {
                TaskId = task.TaskId,
                Status = task.Status,
                StatusMessage = task.StatusMessage,
                CreatedAt = task.CreatedAt,
                LastUpdatedAt = task.LastUpdatedAt,
                TimeToLive = task.TimeToLive,
                PollInterval = task.PollInterval,
            };

        private static CancelLegacyTaskResult ToCancelTaskResult(McpLegacyTask task) =>
            new()
            {
                TaskId = task.TaskId,
                Status = task.Status,
                StatusMessage = task.StatusMessage,
                CreatedAt = task.CreatedAt,
                LastUpdatedAt = task.LastUpdatedAt,
                TimeToLive = task.TimeToLive,
                PollInterval = task.PollInterval,
            };
    }
}
