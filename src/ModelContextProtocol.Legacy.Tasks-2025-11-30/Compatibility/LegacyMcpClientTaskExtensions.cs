using ModelContextProtocol.Legacy.Tasks;
using ModelContextProtocol.Protocol;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using LegacyTasks = ModelContextProtocol.Legacy.Tasks;

namespace ModelContextProtocol.Client;

/// <summary>Provides source-compatible client APIs for the 2025-11-30 Tasks draft.</summary>
/// <remarks>
/// These extension methods retain the 1.x method names for a source migration. They require a
/// client that negotiated <c>2025-11-30</c>; use the current Tasks extension for newer protocols.
/// </remarks>
[Obsolete(LegacyTasksApiObsoletion.Message, DiagnosticId = LegacyTasksApiObsoletion.DiagnosticId, UrlFormat = LegacyTasksApiObsoletion.Url)]
public static class LegacyMcpClientTaskExtensions
{
    /// <summary>Invokes a tool as a legacy task.</summary>
    [RequiresDynamicCode("The legacy object-based argument signature requires runtime JSON serialization.")]
    [RequiresUnreferencedCode("The legacy object-based argument signature requires runtime JSON serialization.")]
    public static async ValueTask<McpTask> CallToolAsTaskAsync(
        this McpClient client,
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments = null,
        McpTaskMetadata? taskMetadata = null,
        IProgress<ProgressNotificationValue>? progress = null,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (toolName is null)
        {
            throw new ArgumentNullException(nameof(toolName));
        }

        var requestParams = new CallToolRequestParams
        {
            Name = toolName,
            Arguments = SerializeArguments(arguments, options?.JsonSerializerOptions ?? McpJsonUtilities.DefaultOptions),
            Meta = options?.GetMetaForRequest(),
        };

        if (progress is null)
        {
            return ToCompatibilityTask(await client.CallToolAsLegacyTaskAsync(
                requestParams,
                ToLegacyTaskMetadata(taskMetadata),
                cancellationToken).ConfigureAwait(false));
        }

        var progressToken = new ProgressToken(Guid.NewGuid().ToString("N"));
        requestParams.Meta = requestParams.Meta is null ? [] : (JsonObject)requestParams.Meta.DeepClone();
        requestParams.Meta["progressToken"] = progressToken.ToString();

        await using var registration = client.RegisterNotificationHandler(
            NotificationMethods.ProgressNotification,
            (notification, _) =>
            {
                if (JsonSerializer.Deserialize(notification.Params, McpJsonUtilities.DefaultOptions.GetTypeInfo<ProgressNotificationParams>()) is { } progressNotification &&
                    progressNotification.ProgressToken == progressToken)
                {
                    progress.Report(progressNotification.Progress);
                }

                return default;
            });

        return ToCompatibilityTask(await client.CallToolAsLegacyTaskAsync(
            requestParams,
            ToLegacyTaskMetadata(taskMetadata),
            cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Gets the current state of a legacy task.</summary>
    public static async ValueTask<McpTask> GetTaskAsync(
        this McpClient client,
        string taskId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new ArgumentException("The task identifier must not be empty.", nameof(taskId));
        }

        return ToCompatibilityTask(await client.GetLegacyTaskAsync(taskId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Gets the result payload of a legacy task.</summary>
    public static ValueTask<JsonElement> GetTaskResultAsync(
        this McpClient client,
        string taskId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new ArgumentException("The task identifier must not be empty.", nameof(taskId));
        }

        return client.GetLegacyTaskPayloadAsync(taskId, cancellationToken);
    }

    /// <summary>Lists every legacy task visible to the client.</summary>
    public static async ValueTask<IList<McpTask>> ListTasksAsync(
        this McpClient client,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        List<McpTask> tasks = [];
        string? cursor = null;
        do
        {
            var page = await client.ListLegacyTasksAsync(cursor, cancellationToken).ConfigureAwait(false);
            tasks.AddRange(page.Tasks.Select(ToCompatibilityTask));
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        return tasks;
    }

    /// <summary>Lists one page of legacy tasks.</summary>
    public static async ValueTask<ListTasksResult> ListTasksAsync(
        this McpClient client,
        ListTasksRequestParams requestParams,
        CancellationToken cancellationToken = default)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (requestParams is null)
        {
            throw new ArgumentNullException(nameof(requestParams));
        }

        var page = await client.ListLegacyTasksAsync(requestParams.Cursor, cancellationToken).ConfigureAwait(false);
        return new ListTasksResult
        {
            Tasks = page.Tasks.Select(ToCompatibilityTask).ToList(),
            NextCursor = page.NextCursor,
        };
    }

    /// <summary>Cancels a legacy task.</summary>
    public static async ValueTask<McpTask> CancelTaskAsync(
        this McpClient client,
        string taskId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new ArgumentException("The task identifier must not be empty.", nameof(taskId));
        }

        return ToCompatibilityTask(await client.CancelLegacyTaskAsync(taskId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Polls a legacy task until it reaches a terminal state.</summary>
    public static async ValueTask<McpTask> PollTaskUntilCompleteAsync(
        this McpClient client,
        string taskId,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new ArgumentException("The task identifier must not be empty.", nameof(taskId));
        }

        while (true)
        {
            var task = await client.GetTaskAsync(taskId, options, cancellationToken).ConfigureAwait(false);
            if (task.Status is McpTaskStatus.Completed or McpTaskStatus.Failed or McpTaskStatus.Cancelled)
            {
                return task;
            }

            await Task.Delay(task.PollInterval ?? TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }
    }

    [RequiresDynamicCode("The legacy object-based argument signature requires runtime JSON serialization.")]
    [RequiresUnreferencedCode("The legacy object-based argument signature requires runtime JSON serialization.")]
    private static IDictionary<string, JsonElement>? SerializeArguments(
        IReadOnlyDictionary<string, object?>? arguments,
        JsonSerializerOptions serializerOptions)
    {
        if (arguments is null)
        {
            return null;
        }

        var serializedArguments = new Dictionary<string, JsonElement>(arguments.Count, StringComparer.Ordinal);
        foreach (var argument in arguments)
        {
            serializedArguments.Add(argument.Key, JsonSerializer.SerializeToElement(argument.Value, serializerOptions));
        }

        return serializedArguments;
    }

    private static LegacyTasks.McpLegacyTaskMetadata ToLegacyTaskMetadata(McpTaskMetadata? metadata) =>
        new() { TimeToLive = metadata?.TimeToLive };

    private static McpTask ToCompatibilityTask(LegacyTasks.McpLegacyTask task) =>
        ToCompatibilityTask(
            task.TaskId,
            task.Status,
            task.StatusMessage,
            task.CreatedAt,
            task.LastUpdatedAt,
            task.TimeToLive,
            task.PollInterval);

    private static McpTask ToCompatibilityTask(LegacyTasks.GetLegacyTaskResult task) =>
        ToCompatibilityTask(
            task.TaskId,
            task.Status,
            task.StatusMessage,
            task.CreatedAt,
            task.LastUpdatedAt,
            task.TimeToLive,
            task.PollInterval);

    private static McpTask ToCompatibilityTask(LegacyTasks.CancelLegacyTaskResult task) =>
        ToCompatibilityTask(
            task.TaskId,
            task.Status,
            task.StatusMessage,
            task.CreatedAt,
            task.LastUpdatedAt,
            task.TimeToLive,
            task.PollInterval);

    private static McpTask ToCompatibilityTask(
        string taskId,
        LegacyTasks.McpLegacyTaskStatus status,
        string? statusMessage,
        DateTimeOffset createdAt,
        DateTimeOffset lastUpdatedAt,
        TimeSpan? timeToLive,
        TimeSpan? pollInterval) =>
        new()
        {
            TaskId = taskId,
            Status = status switch
            {
                LegacyTasks.McpLegacyTaskStatus.Working => McpTaskStatus.Working,
                LegacyTasks.McpLegacyTaskStatus.InputRequired => McpTaskStatus.InputRequired,
                LegacyTasks.McpLegacyTaskStatus.Completed => McpTaskStatus.Completed,
                LegacyTasks.McpLegacyTaskStatus.Failed => McpTaskStatus.Failed,
                LegacyTasks.McpLegacyTaskStatus.Cancelled => McpTaskStatus.Cancelled,
                _ => throw new InvalidOperationException($"Unknown legacy task status '{status}'."),
            },
            StatusMessage = statusMessage,
            CreatedAt = createdAt,
            LastUpdatedAt = lastUpdatedAt,
            TimeToLive = timeToLive,
            PollInterval = pollInterval,
        };

    private static McpTask ToCompatibilityTask(LegacyTasks.LegacyTaskCallResult result) =>
        result.Task is { } task
            ? ToCompatibilityTask(task)
            : throw new InvalidOperationException("The legacy server returned an immediate tool result instead of a task.");
}
