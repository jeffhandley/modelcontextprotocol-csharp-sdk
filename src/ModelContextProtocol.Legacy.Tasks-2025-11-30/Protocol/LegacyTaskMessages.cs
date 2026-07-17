using ModelContextProtocol.Protocol;
using System.Text.Json.Serialization;

namespace ModelContextProtocol.Legacy.Tasks;

/// <summary>Represents the result returned when a task-augmented request is accepted.</summary>
public sealed class CreateLegacyTaskResult : Result
{
    /// <summary>Gets or sets the newly created task.</summary>
    [JsonPropertyName("task")]
    public required McpLegacyTask Task { get; set; }
}

/// <summary>Represents the parameters for a legacy <c>tasks/get</c> request.</summary>
public sealed class GetLegacyTaskRequestParams
{
    /// <summary>Gets or sets the task identifier.</summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; set; }
}

/// <summary>Represents the result of a legacy <c>tasks/get</c> request.</summary>
public sealed class GetLegacyTaskResult
{
    /// <summary>Gets or sets the task identifier.</summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; set; }

    /// <summary>Gets or sets the current task status.</summary>
    [JsonPropertyName("status")]
    public required McpLegacyTaskStatus Status { get; set; }

    /// <summary>Gets or sets a human-readable status message.</summary>
    [JsonPropertyName("statusMessage")]
    public string? StatusMessage { get; set; }

    /// <summary>Gets or sets when the task was created.</summary>
    [JsonPropertyName("createdAt")]
    public required DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets when the task was last updated.</summary>
    [JsonPropertyName("lastUpdatedAt")]
    public required DateTimeOffset LastUpdatedAt { get; set; }

    /// <summary>Gets or sets the task retention period.</summary>
    [JsonPropertyName("ttl")]
    [JsonConverter(typeof(LegacyTimeSpanMillisecondsConverter))]
    public TimeSpan? TimeToLive { get; set; }

    /// <summary>Gets or sets the suggested status polling interval.</summary>
    [JsonPropertyName("pollInterval")]
    [JsonConverter(typeof(LegacyTimeSpanMillisecondsConverter))]
    public TimeSpan? PollInterval { get; set; }
}

/// <summary>Represents the parameters for a legacy <c>tasks/list</c> request.</summary>
public sealed class ListLegacyTasksRequestParams
{
    /// <summary>Gets or sets the optional pagination cursor.</summary>
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }
}

/// <summary>Represents the result of a legacy <c>tasks/list</c> request.</summary>
public sealed class ListLegacyTasksResult
{
    /// <summary>Gets or sets the tasks in the current page.</summary>
    [JsonPropertyName("tasks")]
    public required IList<McpLegacyTask> Tasks { get; set; }

    /// <summary>Gets or sets the optional next-page cursor.</summary>
    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }
}

/// <summary>Represents the parameters for a legacy <c>tasks/result</c> request.</summary>
public sealed class GetLegacyTaskPayloadRequestParams
{
    /// <summary>Gets or sets the task identifier.</summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; set; }
}

/// <summary>Represents the parameters for a legacy <c>tasks/cancel</c> request.</summary>
public sealed class CancelLegacyTaskRequestParams
{
    /// <summary>Gets or sets the task identifier.</summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; set; }
}

/// <summary>Represents the result of a legacy <c>tasks/cancel</c> request.</summary>
public sealed class CancelLegacyTaskResult
{
    /// <summary>Gets or sets the task identifier.</summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; set; }

    /// <summary>Gets or sets the task status.</summary>
    [JsonPropertyName("status")]
    public required McpLegacyTaskStatus Status { get; set; }

    /// <summary>Gets or sets a human-readable status message.</summary>
    [JsonPropertyName("statusMessage")]
    public string? StatusMessage { get; set; }

    /// <summary>Gets or sets when the task was created.</summary>
    [JsonPropertyName("createdAt")]
    public required DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets when the task was last updated.</summary>
    [JsonPropertyName("lastUpdatedAt")]
    public required DateTimeOffset LastUpdatedAt { get; set; }

    /// <summary>Gets or sets the task retention period.</summary>
    [JsonPropertyName("ttl")]
    [JsonConverter(typeof(LegacyTimeSpanMillisecondsConverter))]
    public TimeSpan? TimeToLive { get; set; }

    /// <summary>Gets or sets the suggested status polling interval.</summary>
    [JsonPropertyName("pollInterval")]
    [JsonConverter(typeof(LegacyTimeSpanMillisecondsConverter))]
    public TimeSpan? PollInterval { get; set; }
}

/// <summary>Represents a legacy task status notification.</summary>
public sealed class LegacyTaskStatusNotificationParams
{
    /// <summary>Gets or sets the task identifier.</summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; set; }

    /// <summary>Gets or sets the current task status.</summary>
    [JsonPropertyName("status")]
    public required McpLegacyTaskStatus Status { get; set; }

    /// <summary>Gets or sets a human-readable status message.</summary>
    [JsonPropertyName("statusMessage")]
    public string? StatusMessage { get; set; }

    /// <summary>Gets or sets when the task was created.</summary>
    [JsonPropertyName("createdAt")]
    public required DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets when the task was last updated.</summary>
    [JsonPropertyName("lastUpdatedAt")]
    public required DateTimeOffset LastUpdatedAt { get; set; }

    /// <summary>Gets or sets the task retention period.</summary>
    [JsonPropertyName("ttl")]
    [JsonConverter(typeof(LegacyTimeSpanMillisecondsConverter))]
    public TimeSpan? TimeToLive { get; set; }

    /// <summary>Gets or sets the suggested status polling interval.</summary>
    [JsonPropertyName("pollInterval")]
    [JsonConverter(typeof(LegacyTimeSpanMillisecondsConverter))]
    public TimeSpan? PollInterval { get; set; }
}
