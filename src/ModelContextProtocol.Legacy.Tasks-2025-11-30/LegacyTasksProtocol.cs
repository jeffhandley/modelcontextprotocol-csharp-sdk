namespace ModelContextProtocol.Legacy.Tasks;

/// <summary>
/// Constants for the experimental MCP Tasks protocol revision from 2025-11-30.
/// </summary>
public static class LegacyTasksProtocol
{
    /// <summary>The protocol revision that defined this legacy Tasks draft.</summary>
    public const string ProtocolVersion = "2025-11-30";

    /// <summary>The top-level capability property used by the legacy Tasks draft.</summary>
    public const string CapabilityName = "tasks";

    /// <summary>The legacy request augmentation property.</summary>
    public const string TaskPropertyName = "task";

    /// <summary>The request method for reading a task's status.</summary>
    public const string GetTaskMethod = "tasks/get";

    /// <summary>The request method for listing tasks.</summary>
    public const string ListTasksMethod = "tasks/list";

    /// <summary>The request method for retrieving a completed task's payload.</summary>
    public const string GetTaskResultMethod = "tasks/result";

    /// <summary>The request method for cancelling a task.</summary>
    public const string CancelTaskMethod = "tasks/cancel";

    /// <summary>The optional notification sent when a task's status changes.</summary>
    public const string TaskStatusNotificationMethod = "notifications/tasks/status";
}
