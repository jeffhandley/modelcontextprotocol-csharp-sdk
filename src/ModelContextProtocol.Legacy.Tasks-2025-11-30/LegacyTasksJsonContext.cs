using System.Text.Json.Serialization;

namespace ModelContextProtocol.Legacy.Tasks;

/// <summary>Provides source-generated JSON serialization metadata for legacy Tasks messages.</summary>
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(McpLegacyTask))]
[JsonSerializable(typeof(McpLegacyTaskMetadata))]
[JsonSerializable(typeof(CreateLegacyTaskResult))]
[JsonSerializable(typeof(GetLegacyTaskRequestParams))]
[JsonSerializable(typeof(GetLegacyTaskResult))]
[JsonSerializable(typeof(ListLegacyTasksRequestParams))]
[JsonSerializable(typeof(ListLegacyTasksResult))]
[JsonSerializable(typeof(GetLegacyTaskPayloadRequestParams))]
[JsonSerializable(typeof(CancelLegacyTaskRequestParams))]
[JsonSerializable(typeof(CancelLegacyTaskResult))]
[JsonSerializable(typeof(LegacyTaskStatusNotificationParams))]
public sealed partial class LegacyTasksJsonContext : JsonSerializerContext
{
}
