using ModelContextProtocol.Legacy.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModelContextProtocol.Protocol;

/// <summary>Represents a task from the 2025-11-30 Tasks draft.</summary>
[Obsolete(LegacyTasksApiObsoletion.Message, DiagnosticId = LegacyTasksApiObsoletion.DiagnosticId, UrlFormat = LegacyTasksApiObsoletion.Url)]
public sealed class McpTask
{
    /// <summary>Gets or sets the task identifier.</summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; set; }

    /// <summary>Gets or sets the task status.</summary>
    [JsonPropertyName("status")]
    public required McpTaskStatus Status { get; set; }

    /// <summary>Gets or sets an optional task status message.</summary>
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
    [JsonConverter(typeof(LegacyCompatibilityTimeSpanMillisecondsConverter))]
    public TimeSpan? TimeToLive { get; set; }

    /// <summary>Gets or sets the suggested status-polling interval.</summary>
    [JsonPropertyName("pollInterval")]
    [JsonConverter(typeof(LegacyCompatibilityTimeSpanMillisecondsConverter))]
    public TimeSpan? PollInterval { get; set; }
}

/// <summary>Represents a status from the 2025-11-30 Tasks draft.</summary>
[JsonConverter(typeof(LegacyCompatibilityTaskStatusConverter))]
[Obsolete(LegacyTasksApiObsoletion.Message, DiagnosticId = LegacyTasksApiObsoletion.DiagnosticId, UrlFormat = LegacyTasksApiObsoletion.Url)]
public enum McpTaskStatus
{
    /// <summary>The task is executing.</summary>
    Working,

    /// <summary>The task requires input.</summary>
    InputRequired,

    /// <summary>The task completed successfully.</summary>
    Completed,

    /// <summary>The task failed.</summary>
    Failed,

    /// <summary>The task was cancelled.</summary>
    Cancelled,
}

/// <summary>Represents metadata that augments a legacy task request.</summary>
[Obsolete(LegacyTasksApiObsoletion.Message, DiagnosticId = LegacyTasksApiObsoletion.DiagnosticId, UrlFormat = LegacyTasksApiObsoletion.Url)]
public sealed class McpTaskMetadata
{
    /// <summary>Gets or sets the requested task retention period.</summary>
    [JsonPropertyName("ttl")]
    [JsonConverter(typeof(LegacyCompatibilityTimeSpanMillisecondsConverter))]
    public TimeSpan? TimeToLive { get; set; }
}

/// <summary>Represents the alternate result returned for a task-augmented request.</summary>
[Obsolete(LegacyTasksApiObsoletion.Message, DiagnosticId = LegacyTasksApiObsoletion.DiagnosticId, UrlFormat = LegacyTasksApiObsoletion.Url)]
public sealed class CreateTaskResult : Result
{
    /// <summary>Gets or sets the created task.</summary>
    [JsonPropertyName("task")]
    public required McpTask Task { get; set; }
}

/// <summary>Represents parameters for a legacy <c>tasks/get</c> request.</summary>
[Obsolete(LegacyTasksApiObsoletion.Message, DiagnosticId = LegacyTasksApiObsoletion.DiagnosticId, UrlFormat = LegacyTasksApiObsoletion.Url)]
public sealed class GetTaskRequestParams : RequestParams
{
    /// <summary>Gets or sets the task identifier.</summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; set; }
}

/// <summary>Represents the result of a legacy <c>tasks/get</c> request.</summary>
[Obsolete(LegacyTasksApiObsoletion.Message, DiagnosticId = LegacyTasksApiObsoletion.DiagnosticId, UrlFormat = LegacyTasksApiObsoletion.Url)]
public sealed class GetTaskResult : Result
{
    /// <summary>Gets or sets the task identifier.</summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; set; }

    /// <summary>Gets or sets the task status.</summary>
    [JsonPropertyName("status")]
    public required McpTaskStatus Status { get; set; }

    /// <summary>Gets or sets an optional task status message.</summary>
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
    [JsonConverter(typeof(LegacyCompatibilityTimeSpanMillisecondsConverter))]
    public TimeSpan? TimeToLive { get; set; }

    /// <summary>Gets or sets the suggested status-polling interval.</summary>
    [JsonPropertyName("pollInterval")]
    [JsonConverter(typeof(LegacyCompatibilityTimeSpanMillisecondsConverter))]
    public TimeSpan? PollInterval { get; set; }
}

/// <summary>Represents parameters for a legacy <c>tasks/result</c> request.</summary>
[Obsolete(LegacyTasksApiObsoletion.Message, DiagnosticId = LegacyTasksApiObsoletion.DiagnosticId, UrlFormat = LegacyTasksApiObsoletion.Url)]
public sealed class GetTaskPayloadRequestParams : RequestParams
{
    /// <summary>Gets or sets the task identifier.</summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; set; }
}

/// <summary>Represents parameters for a legacy <c>tasks/list</c> request.</summary>
[Obsolete(LegacyTasksApiObsoletion.Message, DiagnosticId = LegacyTasksApiObsoletion.DiagnosticId, UrlFormat = LegacyTasksApiObsoletion.Url)]
public sealed class ListTasksRequestParams
{
    /// <summary>Gets or sets the optional pagination cursor.</summary>
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }
}

/// <summary>Represents the result of a legacy <c>tasks/list</c> request.</summary>
[Obsolete(LegacyTasksApiObsoletion.Message, DiagnosticId = LegacyTasksApiObsoletion.DiagnosticId, UrlFormat = LegacyTasksApiObsoletion.Url)]
public sealed class ListTasksResult
{
    /// <summary>Gets or sets the tasks in the current page.</summary>
    [JsonPropertyName("tasks")]
    public required IList<McpTask> Tasks { get; set; }

    /// <summary>Gets or sets the optional next-page cursor.</summary>
    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }
}

/// <summary>Represents parameters for a legacy <c>tasks/cancel</c> request.</summary>
[Obsolete(LegacyTasksApiObsoletion.Message, DiagnosticId = LegacyTasksApiObsoletion.DiagnosticId, UrlFormat = LegacyTasksApiObsoletion.Url)]
public sealed class CancelMcpTaskRequestParams : RequestParams
{
    /// <summary>Gets or sets the task identifier.</summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; set; }
}

/// <summary>Represents the result of a legacy <c>tasks/cancel</c> request.</summary>
[Obsolete(LegacyTasksApiObsoletion.Message, DiagnosticId = LegacyTasksApiObsoletion.DiagnosticId, UrlFormat = LegacyTasksApiObsoletion.Url)]
public sealed class CancelMcpTaskResult : Result
{
    /// <summary>Gets or sets the task identifier.</summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; set; }

    /// <summary>Gets or sets the task status.</summary>
    [JsonPropertyName("status")]
    public required McpTaskStatus Status { get; set; }

    /// <summary>Gets or sets an optional task status message.</summary>
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
    [JsonConverter(typeof(LegacyCompatibilityTimeSpanMillisecondsConverter))]
    public TimeSpan? TimeToLive { get; set; }

    /// <summary>Gets or sets the suggested status-polling interval.</summary>
    [JsonPropertyName("pollInterval")]
    [JsonConverter(typeof(LegacyCompatibilityTimeSpanMillisecondsConverter))]
    public TimeSpan? PollInterval { get; set; }
}

/// <summary>Represents a legacy task status notification.</summary>
[Obsolete(LegacyTasksApiObsoletion.Message, DiagnosticId = LegacyTasksApiObsoletion.DiagnosticId, UrlFormat = LegacyTasksApiObsoletion.Url)]
public sealed class McpTaskStatusNotificationParams : NotificationParams
{
    /// <summary>Gets or sets the task identifier.</summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; set; }

    /// <summary>Gets or sets the task status.</summary>
    [JsonPropertyName("status")]
    public required McpTaskStatus Status { get; set; }

    /// <summary>Gets or sets an optional task status message.</summary>
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
    [JsonConverter(typeof(LegacyCompatibilityTimeSpanMillisecondsConverter))]
    public TimeSpan? TimeToLive { get; set; }

    /// <summary>Gets or sets the suggested status-polling interval.</summary>
    [JsonPropertyName("pollInterval")]
    [JsonConverter(typeof(LegacyCompatibilityTimeSpanMillisecondsConverter))]
    public TimeSpan? PollInterval { get; set; }
}

/// <summary>Represents the legacy top-level Tasks capability.</summary>
[Obsolete(LegacyTasksApiObsoletion.Message, DiagnosticId = LegacyTasksApiObsoletion.DiagnosticId, UrlFormat = LegacyTasksApiObsoletion.Url)]
public sealed class McpTasksCapability
{
    /// <summary>Gets or sets list support.</summary>
    [JsonPropertyName("list")]
    public ListMcpTasksCapability? List { get; set; }

    /// <summary>Gets or sets cancel support.</summary>
    [JsonPropertyName("cancel")]
    public CancelMcpTasksCapability? Cancel { get; set; }

    /// <summary>Gets or sets task-augmented request support.</summary>
    [JsonPropertyName("requests")]
    public RequestMcpTasksCapability? Requests { get; set; }
}

/// <summary>Represents legacy task support by request family.</summary>
[Obsolete(LegacyTasksApiObsoletion.Message, DiagnosticId = LegacyTasksApiObsoletion.DiagnosticId, UrlFormat = LegacyTasksApiObsoletion.Url)]
public sealed class RequestMcpTasksCapability
{
    /// <summary>Gets or sets tool task support.</summary>
    [JsonPropertyName("tools")]
    public ToolsMcpTasksCapability? Tools { get; set; }

    /// <summary>Gets or sets sampling task support.</summary>
    [JsonPropertyName("sampling")]
    public SamplingMcpTasksCapability? Sampling { get; set; }

    /// <summary>Gets or sets elicitation task support.</summary>
    [JsonPropertyName("elicitation")]
    public ElicitationMcpTasksCapability? Elicitation { get; set; }
}

/// <summary>Represents legacy tool task support.</summary>
[Obsolete(LegacyTasksApiObsoletion.Message, DiagnosticId = LegacyTasksApiObsoletion.DiagnosticId, UrlFormat = LegacyTasksApiObsoletion.Url)]
public sealed class ToolsMcpTasksCapability
{
    /// <summary>Gets or sets task support for <c>tools/call</c>.</summary>
    [JsonPropertyName("call")]
    public CallToolMcpTasksCapability? Call { get; set; }
}

/// <summary>Represents legacy sampling task support.</summary>
[Obsolete(LegacyTasksApiObsoletion.Message, DiagnosticId = LegacyTasksApiObsoletion.DiagnosticId, UrlFormat = LegacyTasksApiObsoletion.Url)]
public sealed class SamplingMcpTasksCapability
{
    /// <summary>Gets or sets task support for <c>sampling/createMessage</c>.</summary>
    [JsonPropertyName("createMessage")]
    public CreateMessageMcpTasksCapability? CreateMessage { get; set; }
}

/// <summary>Represents legacy elicitation task support.</summary>
[Obsolete(LegacyTasksApiObsoletion.Message, DiagnosticId = LegacyTasksApiObsoletion.DiagnosticId, UrlFormat = LegacyTasksApiObsoletion.Url)]
public sealed class ElicitationMcpTasksCapability
{
    /// <summary>Gets or sets task support for <c>elicitation/create</c>.</summary>
    [JsonPropertyName("create")]
    public CreateElicitationMcpTasksCapability? Create { get; set; }
}

/// <summary>Represents legacy list-task support.</summary>
[Obsolete(LegacyTasksApiObsoletion.Message, DiagnosticId = LegacyTasksApiObsoletion.DiagnosticId, UrlFormat = LegacyTasksApiObsoletion.Url)]
public sealed class ListMcpTasksCapability;

/// <summary>Represents legacy cancel-task support.</summary>
[Obsolete(LegacyTasksApiObsoletion.Message, DiagnosticId = LegacyTasksApiObsoletion.DiagnosticId, UrlFormat = LegacyTasksApiObsoletion.Url)]
public sealed class CancelMcpTasksCapability;

/// <summary>Represents legacy task support for <c>tools/call</c>.</summary>
[Obsolete(LegacyTasksApiObsoletion.Message, DiagnosticId = LegacyTasksApiObsoletion.DiagnosticId, UrlFormat = LegacyTasksApiObsoletion.Url)]
public sealed class CallToolMcpTasksCapability;

/// <summary>Represents legacy task support for <c>sampling/createMessage</c>.</summary>
[Obsolete(LegacyTasksApiObsoletion.Message, DiagnosticId = LegacyTasksApiObsoletion.DiagnosticId, UrlFormat = LegacyTasksApiObsoletion.Url)]
public sealed class CreateMessageMcpTasksCapability;

/// <summary>Represents legacy task support for <c>elicitation/create</c>.</summary>
[Obsolete(LegacyTasksApiObsoletion.Message, DiagnosticId = LegacyTasksApiObsoletion.DiagnosticId, UrlFormat = LegacyTasksApiObsoletion.Url)]
public sealed class CreateElicitationMcpTasksCapability;

internal sealed class LegacyCompatibilityTaskStatusConverter : JsonConverter<McpTaskStatus>
{
    public override McpTaskStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "working" => McpTaskStatus.Working,
            "input_required" => McpTaskStatus.InputRequired,
            "completed" => McpTaskStatus.Completed,
            "failed" => McpTaskStatus.Failed,
            "cancelled" => McpTaskStatus.Cancelled,
            var value => throw new JsonException($"Unknown legacy task status '{value}'."),
        };

    public override void Write(Utf8JsonWriter writer, McpTaskStatus value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            McpTaskStatus.Working => "working",
            McpTaskStatus.InputRequired => "input_required",
            McpTaskStatus.Completed => "completed",
            McpTaskStatus.Failed => "failed",
            McpTaskStatus.Cancelled => "cancelled",
            _ => throw new JsonException($"Unknown legacy task status '{value}'."),
        });
}

internal sealed class LegacyCompatibilityTimeSpanMillisecondsConverter : JsonConverter<TimeSpan?>
{
    public override TimeSpan? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number when reader.TryGetInt64(out long milliseconds) => TimeSpan.FromMilliseconds(milliseconds),
            _ => throw new JsonException("Legacy task durations must be integer milliseconds."),
        };

    public override void Write(Utf8JsonWriter writer, TimeSpan? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteNumberValue((long)value.Value.TotalMilliseconds);
    }
}
