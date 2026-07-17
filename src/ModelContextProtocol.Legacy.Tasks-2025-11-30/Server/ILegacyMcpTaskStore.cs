using System.Text.Json;

namespace ModelContextProtocol.Legacy.Tasks;

/// <summary>Defines durable storage operations required by the legacy Tasks protocol.</summary>
public interface ILegacyMcpTaskStore
{
    /// <summary>Creates a task for an augmented request.</summary>
    Task<McpLegacyTask> CreateTaskAsync(
        McpLegacyTaskMetadata metadata,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the current state of a task.</summary>
    Task<McpLegacyTask?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>Stores the terminal result of a task.</summary>
    Task<McpLegacyTask> StoreTaskResultAsync(
        string taskId,
        McpLegacyTaskStatus status,
        JsonElement result,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the result payload of a terminal task.</summary>
    Task<JsonElement> GetTaskResultAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>Lists the tasks that are visible to the caller.</summary>
    Task<ListLegacyTasksResult> ListTasksAsync(
        string? cursor = null,
        CancellationToken cancellationToken = default);

    /// <summary>Cancels a task if it has not reached a terminal state.</summary>
    Task<McpLegacyTask> CancelTaskAsync(string taskId, CancellationToken cancellationToken = default);
}
