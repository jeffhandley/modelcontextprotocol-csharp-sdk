using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ModelContextProtocol.Extensions.Tasks;

/// <summary>
/// Provides extension methods that add MCP Tasks extension (SEP-2663) support to an MCP client.
/// </summary>
/// <remarks>
/// These methods let a client request task-augmented tool execution and drive the task lifecycle
/// (<c>tasks/get</c>, <c>tasks/update</c>, <c>tasks/cancel</c>). The Tasks extension is draft-only.
/// </remarks>
[Experimental(Experimentals.Tasks_DiagnosticId, UrlFormat = Experimentals.Tasks_Url)]
public static class McpClientTasksExtensions
{
    /// <summary>The default number of consecutive stuck polls tolerated before a task is abandoned.</summary>
    public const int DefaultMaxConsecutiveStuckPolls = 60;

    /// <summary>
    /// Invokes a tool on the server with task extension support, transparently polling to completion.
    /// </summary>
    /// <param name="client">The client to invoke the tool on.</param>
    /// <param name="requestParams">The request parameters to send in the request.</param>
    /// <param name="maxConsecutiveStuckPolls">
    /// The maximum number of consecutive polls in which the task remains in <see cref="McpTaskStatus.InputRequired"/>
    /// without publishing new input requests before the task is abandoned. Must be positive.
    /// </param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The result of the tool invocation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="requestParams"/> is <see langword="null"/>.</exception>
    /// <exception cref="McpException">The request failed or the server returned an error response.</exception>
    /// <remarks>
    /// This method includes the <c>io.modelcontextprotocol/tasks</c> extension capability in the request
    /// metadata. If the server returns a task handle, this method transparently polls <c>tasks/get</c> until
    /// the task completes, fails, or is cancelled. Use <see cref="CallToolRawAsync"/> to disable polling.
    /// </remarks>
    public static async ValueTask<CallToolResult> CallToolAsTaskAsync(
        this McpClient client,
        CallToolRequestParams requestParams,
        int maxConsecutiveStuckPolls = DefaultMaxConsecutiveStuckPolls,
        CancellationToken cancellationToken = default)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        if (requestParams is null) throw new ArgumentNullException(nameof(requestParams));
        if (maxConsecutiveStuckPolls <= 0) throw new ArgumentOutOfRangeException(nameof(maxConsecutiveStuckPolls));

        var augmented = await client.CallToolRawAsync(requestParams, cancellationToken).ConfigureAwait(false);

        if (!augmented.IsTask)
        {
            return augmented.Result!;
        }

        return await client.PollTaskToCompletionAsync(augmented.TaskCreated!, maxConsecutiveStuckPolls, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Invokes a tool on the server with task extension support, returning the raw response without polling.
    /// </summary>
    /// <param name="client">The client to invoke the tool on.</param>
    /// <param name="requestParams">The request parameters to send. The tasks extension capability is injected into the request metadata.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="ResultOrCreatedTask{TResult}"/> that is either an immediate result or a task handle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="requestParams"/> is <see langword="null"/>.</exception>
    /// <exception cref="McpException">The request failed or the server returned an error response.</exception>
    public static async ValueTask<ResultOrCreatedTask<CallToolResult>> CallToolRawAsync(
        this McpClient client,
        CallToolRequestParams requestParams,
        CancellationToken cancellationToken = default)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        if (requestParams is null) throw new ArgumentNullException(nameof(requestParams));

        var paramsWithMeta = new CallToolRequestParams
        {
            Name = requestParams.Name,
            Arguments = requestParams.Arguments,
            // The SEP-2663 Tasks extension is draft-only. On a legacy session, send a plain tools/call
            // (no task capability envelope) so the server returns a direct CallToolResult.
            Meta = client.IsDraftProtocol() ? GetMetaWithTaskCapability(requestParams.Meta) : requestParams.Meta,
        };

        JsonRpcRequest jsonRpcRequest = new()
        {
            Method = RequestMethods.ToolsCall,
            Params = JsonSerializer.SerializeToNode(paramsWithMeta, TasksJsonContext.Default.CallToolRequestParams),
        };

        JsonRpcResponse response = await client.SendRequestAsync(jsonRpcRequest, cancellationToken).ConfigureAwait(false);

        // Discriminate based on resultType field.
        if (response.Result is JsonObject resultObj &&
            resultObj.TryGetPropertyValue("resultType", out var resultTypeNode) &&
            resultTypeNode?.GetValue<string>() == "task")
        {
            var taskCreated = resultObj.Deserialize(TasksJsonContext.Default.CreateTaskResult)
                ?? throw new JsonException("Failed to deserialize CreateTaskResult from response.");
            return new ResultOrCreatedTask<CallToolResult>(taskCreated);
        }

        var callToolResult = JsonSerializer.Deserialize(response.Result, TasksJsonContext.Default.CallToolResult)
            ?? throw new JsonException("Failed to deserialize CallToolResult from response.");
        return new ResultOrCreatedTask<CallToolResult>(callToolResult);
    }

    /// <summary>
    /// Polls a task until it reaches a terminal state and returns the final <see cref="CallToolResult"/>.
    /// </summary>
    /// <param name="client">The client polling the task.</param>
    /// <param name="taskCreated">The task handle returned by the server.</param>
    /// <param name="maxConsecutiveStuckPolls">
    /// The maximum number of consecutive polls in which the task remains in <see cref="McpTaskStatus.InputRequired"/>
    /// without publishing new input requests before the task is abandoned. Must be positive.
    /// </param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The final result of the completed task.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="taskCreated"/> is <see langword="null"/>.</exception>
    /// <exception cref="McpException">The task failed or remained stuck.</exception>
    /// <exception cref="OperationCanceledException">The task was cancelled by the server.</exception>
    public static async ValueTask<CallToolResult> PollTaskToCompletionAsync(
        this McpClient client,
        CreateTaskResult taskCreated,
        int maxConsecutiveStuckPolls = DefaultMaxConsecutiveStuckPolls,
        CancellationToken cancellationToken = default)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        if (taskCreated is null) throw new ArgumentNullException(nameof(taskCreated));
        if (maxConsecutiveStuckPolls <= 0) throw new ArgumentOutOfRangeException(nameof(maxConsecutiveStuckPolls));

        string taskId = taskCreated.TaskId;
        long pollIntervalMs = taskCreated.PollIntervalMs ?? 1000;
        HashSet<string>? resolvedRequestKeys = null;
        bool isFirstPoll = true;
        int consecutiveStuckPolls = 0;

        while (true)
        {
            // Skip the delay before the first poll: many tasks complete almost immediately.
            if (!isFirstPoll)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(pollIntervalMs), cancellationToken).ConfigureAwait(false);
            }
            isFirstPoll = false;

            var taskResult = await client.GetTaskAsync(taskId, cancellationToken).ConfigureAwait(false);

            if (taskResult.PollIntervalMs is { } newInterval)
            {
                pollIntervalMs = newInterval;
            }

            switch (taskResult)
            {
                case CompletedTaskResult completed:
                    return JsonSerializer.Deserialize(completed.Result, TasksJsonContext.Default.CallToolResult)
                        ?? throw new JsonException("Failed to deserialize CallToolResult from completed task.");

                case FailedTaskResult failed:
                    throw new McpException($"Task '{taskId}' failed: {failed.Error}");

                case CancelledTaskResult:
                    throw new OperationCanceledException($"Task '{taskId}' was cancelled by the server.");

                case InputRequiredTaskResult inputRequired:
                    var newRequests = new Dictionary<string, InputRequest>();
                    if (inputRequired.InputRequests is { } incomingRequests)
                    {
                        foreach (var kvp in incomingRequests)
                        {
                            if (resolvedRequestKeys is null || !resolvedRequestKeys.Contains(kvp.Key))
                            {
                                newRequests[kvp.Key] = kvp.Value;
                            }
                        }
                    }

                    if (newRequests.Count > 0)
                    {
                        consecutiveStuckPolls = 0;

                        IDictionary<string, InputResponse> inputResponses;
                        try
                        {
                            inputResponses = await client.ResolveInputRequestsAsync(newRequests, cancellationToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch
                        {
                            // The input handler failed. Best-effort cancel of the server-side task so it
                            // doesn't stay stuck in InputRequired until TTL expires.
                            try
                            {
                                await client.CancelTaskAsync(taskId, CancellationToken.None).ConfigureAwait(false);
                            }
                            catch
                            {
                                // Swallow secondary failures; we're already propagating the original exception.
                            }

                            throw;
                        }

                        await client.UpdateTaskAsync(new UpdateTaskRequestParams
                        {
                            TaskId = taskId,
                            InputResponses = inputResponses,
                        }, cancellationToken).ConfigureAwait(false);

                        resolvedRequestKeys ??= new HashSet<string>(StringComparer.Ordinal);
                        foreach (var key in inputResponses.Keys)
                        {
                            resolvedRequestKeys.Add(key);
                        }
                    }
                    else if (++consecutiveStuckPolls >= maxConsecutiveStuckPolls)
                    {
                        try
                        {
                            await client.CancelTaskAsync(taskId, CancellationToken.None).ConfigureAwait(false);
                        }
                        catch
                        {
                            // Swallow secondary failures; we're already propagating an exception.
                        }

                        throw new McpException(
                            $"Task '{taskId}' has remained in '{McpTaskStatus.InputRequired}' for {maxConsecutiveStuckPolls} consecutive polls " +
                            "without publishing new input requests after all previously requested inputs were resolved.");
                    }

                    break;

                case WorkingTaskResult:
                    consecutiveStuckPolls = 0;
                    break;

                default:
                    throw new McpException(
                        $"Unexpected task result type '{taskResult.GetType().Name}' for task '{taskId}'.");
            }
        }
    }

    /// <summary>
    /// Retrieves the current state of a task from the server.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="taskId">The stable identifier of the task to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="GetTaskResult"/> subtype representing the current task state.</returns>
    public static ValueTask<GetTaskResult> GetTaskAsync(
        this McpClient client,
        string taskId,
        CancellationToken cancellationToken = default)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        if (taskId is null) throw new ArgumentNullException(nameof(taskId));

        return client.GetTaskAsync(new GetTaskRequestParams { TaskId = taskId }, cancellationToken);
    }

    /// <summary>
    /// Retrieves the current state of a task from the server.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="requestParams">The request parameters to send in the request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="GetTaskResult"/> subtype representing the current task state.</returns>
    public static async ValueTask<GetTaskResult> GetTaskAsync(
        this McpClient client,
        GetTaskRequestParams requestParams,
        CancellationToken cancellationToken = default)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        if (requestParams is null) throw new ArgumentNullException(nameof(requestParams));
        ThrowIfTasksNotSupported(client, nameof(GetTaskAsync));

        var response = await client.SendRequestAsync(
            new JsonRpcRequest
            {
                Method = TaskMethods.Get,
                Params = JsonSerializer.SerializeToNode(requestParams, TasksJsonContext.Default.GetTaskRequestParams),
            },
            cancellationToken).ConfigureAwait(false);

        return JsonSerializer.Deserialize(response.Result, TasksJsonContext.Default.GetTaskResult)
            ?? throw new JsonException("Failed to deserialize GetTaskResult from response.");
    }

    /// <summary>
    /// Provides input responses to a task that is in the <see cref="McpTaskStatus.InputRequired"/> state.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="requestParams">The request parameters containing the task ID and input responses.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The result acknowledging the update.</returns>
    public static async ValueTask<UpdateTaskResult> UpdateTaskAsync(
        this McpClient client,
        UpdateTaskRequestParams requestParams,
        CancellationToken cancellationToken = default)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        if (requestParams is null) throw new ArgumentNullException(nameof(requestParams));
        ThrowIfTasksNotSupported(client, nameof(UpdateTaskAsync));

        var response = await client.SendRequestAsync(
            new JsonRpcRequest
            {
                Method = TaskMethods.Update,
                Params = JsonSerializer.SerializeToNode(requestParams, TasksJsonContext.Default.UpdateTaskRequestParams),
            },
            cancellationToken).ConfigureAwait(false);

        return JsonSerializer.Deserialize(response.Result, TasksJsonContext.Default.UpdateTaskResult)
            ?? throw new JsonException("Failed to deserialize UpdateTaskResult from response.");
    }

    /// <summary>
    /// Requests cancellation of an in-progress task on the server.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="taskId">The stable identifier of the task to cancel.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The result acknowledging the cancellation request.</returns>
    public static ValueTask<CancelTaskResult> CancelTaskAsync(
        this McpClient client,
        string taskId,
        CancellationToken cancellationToken = default)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        if (taskId is null) throw new ArgumentNullException(nameof(taskId));

        return client.CancelTaskAsync(new CancelTaskRequestParams { TaskId = taskId }, cancellationToken);
    }

    /// <summary>
    /// Requests cancellation of an in-progress task on the server.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="requestParams">The request parameters to send in the request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The result acknowledging the cancellation request.</returns>
    public static async ValueTask<CancelTaskResult> CancelTaskAsync(
        this McpClient client,
        CancelTaskRequestParams requestParams,
        CancellationToken cancellationToken = default)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        if (requestParams is null) throw new ArgumentNullException(nameof(requestParams));
        ThrowIfTasksNotSupported(client, nameof(CancelTaskAsync));

        var response = await client.SendRequestAsync(
            new JsonRpcRequest
            {
                Method = TaskMethods.Cancel,
                Params = JsonSerializer.SerializeToNode(requestParams, TasksJsonContext.Default.CancelTaskRequestParams),
            },
            cancellationToken).ConfigureAwait(false);

        return JsonSerializer.Deserialize(response.Result, TasksJsonContext.Default.CancelTaskResult)
            ?? throw new JsonException("Failed to deserialize CancelTaskResult from response.");
    }

    // Per SEP-2663 51, the per-request opt-in uses the SEP-2575 capabilities envelope:
    //   _meta/io.modelcontextprotocol/clientCapabilities/extensions/io.modelcontextprotocol/tasks = {}
    private static JsonObject GetMetaWithTaskCapability(JsonObject? existingMeta)
    {
        JsonObject meta = existingMeta is not null
            ? (JsonObject)existingMeta.DeepClone()
            : [];

        if (meta[MetaKeys.ClientCapabilities] is not JsonObject capsRoot)
        {
            capsRoot = [];
            meta[MetaKeys.ClientCapabilities] = capsRoot;
        }

        if (capsRoot["extensions"] is not JsonObject extensionsRoot)
        {
            extensionsRoot = [];
            capsRoot["extensions"] = extensionsRoot;
        }

        extensionsRoot.TryAdd(McpExtensions.Tasks, new JsonObject());
        return meta;
    }

    private static void ThrowIfTasksNotSupported(McpClient client, string operationName)
    {
        if (!client.IsDraftProtocol())
        {
            throw new InvalidOperationException(
                $"'{operationName}' requires the draft protocol revision. " +
                $"The negotiated protocol version is '{client.NegotiatedProtocolVersion ?? "(none)"}'. " +
                "The Tasks extension is only available under the draft revision.");
        }
    }
}
