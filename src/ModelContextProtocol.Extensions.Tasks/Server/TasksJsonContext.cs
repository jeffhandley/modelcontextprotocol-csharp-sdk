using ModelContextProtocol.Protocol;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ModelContextProtocol.Extensions.Tasks;

/// <summary>
/// Provides source-generated JSON serialization metadata for MCP Tasks extension types.
/// </summary>
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CreateTaskResult))]
[JsonSerializable(typeof(GetTaskRequestParams))]
[JsonSerializable(typeof(GetTaskResult))]
[JsonSerializable(typeof(WorkingTaskResult))]
[JsonSerializable(typeof(CompletedTaskResult))]
[JsonSerializable(typeof(FailedTaskResult))]
[JsonSerializable(typeof(CancelledTaskResult))]
[JsonSerializable(typeof(InputRequiredTaskResult))]
[JsonSerializable(typeof(UpdateTaskRequestParams))]
[JsonSerializable(typeof(UpdateTaskResult))]
[JsonSerializable(typeof(CancelTaskRequestParams))]
[JsonSerializable(typeof(CancelTaskResult))]
[JsonSerializable(typeof(TaskStatusNotificationParams))]
[JsonSerializable(typeof(WorkingTaskNotificationParams))]
[JsonSerializable(typeof(CompletedTaskNotificationParams))]
[JsonSerializable(typeof(FailedTaskNotificationParams))]
[JsonSerializable(typeof(CancelledTaskNotificationParams))]
[JsonSerializable(typeof(InputRequiredTaskNotificationParams))]
[JsonSerializable(typeof(CallToolRequestParams))]
[JsonSerializable(typeof(CallToolResult))]
[JsonSerializable(typeof(InputRequest))]
[JsonSerializable(typeof(InputResponse))]
[JsonSerializable(typeof(IDictionary<string, InputRequest>))]
[JsonSerializable(typeof(IDictionary<string, InputResponse>))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonRpcErrorDetail))]
internal sealed partial class TasksJsonContext : JsonSerializerContext
{
}
