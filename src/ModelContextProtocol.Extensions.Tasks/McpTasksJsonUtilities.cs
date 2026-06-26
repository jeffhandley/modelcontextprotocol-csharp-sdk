using System.Text.Json;

namespace ModelContextProtocol.Extensions.Tasks;

/// <summary>
/// Provides serialization helpers for the SEP-2663 Tasks extension protocol types.
/// </summary>
public static class McpTasksJsonUtilities
{
    /// <summary>
    /// Gets the <see cref="JsonSerializerOptions"/> used to serialize and deserialize the
    /// Tasks extension protocol types (for example <see cref="ModelContextProtocol.Protocol.CreateTaskResult"/>
    /// and <see cref="ModelContextProtocol.Protocol.GetTaskResult"/>).
    /// </summary>
    /// <remarks>
    /// These options are backed by a source-generated <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>,
    /// so they are safe to use when reflection-based serialization is disabled (for example under Native AOT).
    /// </remarks>
    public static JsonSerializerOptions DefaultOptions { get; } = TasksJsonContext.Default.Options;
}
