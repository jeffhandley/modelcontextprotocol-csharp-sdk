using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModelContextProtocol.Legacy.Tasks;

/// <summary>Represents the state of a task in the 2025-11-30 Tasks draft.</summary>
public sealed class McpLegacyTask
{
    /// <summary>Gets or sets the unique task identifier.</summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; set; }

    /// <summary>Gets or sets the current status.</summary>
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

    /// <summary>Gets or sets the retention period requested or assigned to the task.</summary>
    [JsonPropertyName("ttl")]
    [JsonConverter(typeof(LegacyTimeSpanMillisecondsConverter))]
    public TimeSpan? TimeToLive { get; set; }

    /// <summary>Gets or sets the suggested status polling interval.</summary>
    [JsonPropertyName("pollInterval")]
    [JsonConverter(typeof(LegacyTimeSpanMillisecondsConverter))]
    public TimeSpan? PollInterval { get; set; }
}

/// <summary>Represents the status of a task in the 2025-11-30 Tasks draft.</summary>
[JsonConverter(typeof(LegacyTaskStatusConverter))]
public enum McpLegacyTaskStatus
{
    /// <summary>The task is executing.</summary>
    Working,

    /// <summary>The task needs additional input.</summary>
    InputRequired,

    /// <summary>The task completed successfully.</summary>
    Completed,

    /// <summary>The task completed with an error.</summary>
    Failed,

    /// <summary>The task was cancelled.</summary>
    Cancelled,
}

/// <summary>Provides request metadata that asks a peer to execute a request as a task.</summary>
public sealed class McpLegacyTaskMetadata
{
    /// <summary>Gets or sets the requested retention period.</summary>
    [JsonPropertyName("ttl")]
    [JsonConverter(typeof(LegacyTimeSpanMillisecondsConverter))]
    public TimeSpan? TimeToLive { get; set; }
}

internal sealed class LegacyTaskStatusConverter : JsonConverter<McpLegacyTaskStatus>
{
    public override McpLegacyTaskStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "working" => McpLegacyTaskStatus.Working,
            "input_required" => McpLegacyTaskStatus.InputRequired,
            "completed" => McpLegacyTaskStatus.Completed,
            "failed" => McpLegacyTaskStatus.Failed,
            "cancelled" => McpLegacyTaskStatus.Cancelled,
            var value => throw new JsonException($"Unknown legacy task status '{value}'."),
        };

    public override void Write(Utf8JsonWriter writer, McpLegacyTaskStatus value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            McpLegacyTaskStatus.Working => "working",
            McpLegacyTaskStatus.InputRequired => "input_required",
            McpLegacyTaskStatus.Completed => "completed",
            McpLegacyTaskStatus.Failed => "failed",
            McpLegacyTaskStatus.Cancelled => "cancelled",
            _ => throw new JsonException($"Unknown legacy task status '{value}'."),
        });
}

internal sealed class LegacyTimeSpanMillisecondsConverter : JsonConverter<TimeSpan?>
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
