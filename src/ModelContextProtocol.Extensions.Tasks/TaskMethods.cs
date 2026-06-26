namespace ModelContextProtocol.Extensions.Tasks;

/// <summary>
/// Provides constants for the JSON-RPC methods defined by the MCP Tasks extension (SEP-2663).
/// </summary>
public static class TaskMethods
{
    /// <summary>The <c>tasks/get</c> request method, used to retrieve the current state of a task.</summary>
    public const string Get = "tasks/get";

    /// <summary>The <c>tasks/update</c> request method, used to provide input responses to a task.</summary>
    public const string Update = "tasks/update";

    /// <summary>The <c>tasks/cancel</c> request method, used to request cancellation of a task.</summary>
    public const string Cancel = "tasks/cancel";

    /// <summary>The <c>notifications/tasks/status</c> notification method, used to push task status updates.</summary>
    public const string StatusNotification = "notifications/tasks/status";
}

/// <summary>
/// Provides constants for the <c>_meta</c> keys defined by the MCP Tasks extension (SEP-2663).
/// </summary>
public static class TaskMetaKeys
{
    /// <summary>The <c>_meta</c> key carrying the related task identifier.</summary>
    public const string RelatedTask = "io.modelcontextprotocol/related-task";
}
