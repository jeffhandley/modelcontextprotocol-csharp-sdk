using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using System.Text.Json;

namespace ModelContextProtocol.Legacy.Tasks;

/// <summary>Identifies the Tasks protocol implementation selected after client negotiation.</summary>
public enum McpTaskMigrationMode
{
    /// <summary>The 2025-11-30 legacy Tasks draft is active.</summary>
    Legacy,

    /// <summary>The 2026-07-28 or later Tasks extension is active.</summary>
    Modern,
}

/// <summary>
/// Provides a post-negotiation facade over the legacy and modern MCP Tasks implementations.
/// </summary>
/// <remarks>
/// Create an instance after connecting an <see cref="McpClient"/>. The facade does not alter
/// protocol negotiation; it selects the implementation from the client's negotiated version.
/// </remarks>
public sealed class McpTaskMigrationClient
{
    private readonly McpClient _client;

    internal McpTaskMigrationClient(McpClient client, McpTaskMigrationMode mode)
    {
        _client = client;
        Mode = mode;
    }

    /// <summary>Gets the Tasks implementation selected for this connection.</summary>
    public McpTaskMigrationMode Mode { get; }

    /// <summary>
    /// Gets whether a tool should use task-augmented execution for the selected Tasks implementation.
    /// </summary>
    /// <remarks>
    /// For the legacy draft, this method reads the <c>execution.taskSupport</c> property from the
    /// tool returned by <c>tools/list</c>. For the modern Tasks extension, task support is a
    /// server-level extension capability and applies to all tools registered with that extension.
    /// </remarks>
    public bool SupportsTaskExecution(McpClientTool tool)
    {
        if (tool is null)
        {
            throw new ArgumentNullException(nameof(tool));
        }

        if (Mode == McpTaskMigrationMode.Modern)
        {
            return _client.ServerCapabilities.Extensions?.ContainsKey(TasksProtocol.ExtensionId) is true;
        }

        return tool.ProtocolTool.AdditionalProperties?.TryGetValue("execution", out JsonElement execution) is true &&
            execution.ValueKind == JsonValueKind.Object &&
            execution.TryGetProperty("taskSupport", out JsonElement taskSupport) &&
            taskSupport.ValueKind == JsonValueKind.String &&
            string.Equals(taskSupport.GetString(), "optional", StringComparison.Ordinal);
    }

    /// <summary>
    /// Executes a listed tool using task polling only when the selected protocol indicates that the tool supports it.
    /// </summary>
    /// <param name="tool">The tool description returned from <c>ListToolsAsync</c>.</param>
    /// <param name="requestParams">The parameters for the tool invocation.</param>
    /// <param name="maxConsecutiveStuckPolls">The maximum number of unchanged task status polls to allow.</param>
    /// <param name="cancellationToken">The cancellation token for the invocation.</param>
    /// <returns>The direct or task-polled tool result.</returns>
    public ValueTask<CallToolResult> ExecuteToolAsync(
        McpClientTool tool,
        CallToolRequestParams requestParams,
        int maxConsecutiveStuckPolls = 60,
        CancellationToken cancellationToken = default)
    {
        if (tool is null)
        {
            throw new ArgumentNullException(nameof(tool));
        }

        if (requestParams is null)
        {
            throw new ArgumentNullException(nameof(requestParams));
        }

        if (!string.Equals(tool.ProtocolTool.Name, requestParams.Name, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The request tool name '{requestParams.Name}' does not match the listed tool name '{tool.ProtocolTool.Name}'.",
                nameof(requestParams));
        }

        return SupportsTaskExecution(tool)
            ? CallToolWithPollingAsync(requestParams, maxConsecutiveStuckPolls, cancellationToken)
            : _client.CallToolAsync(requestParams, cancellationToken);
    }

    /// <summary>Calls a tool and returns either its immediate result or a task created by the selected implementation.</summary>
    public async ValueTask<McpTaskMigrationCallResult> CallToolAsTaskAsync(
        CallToolRequestParams requestParams,
        CancellationToken cancellationToken = default)
    {
        if (requestParams is null)
        {
            throw new ArgumentNullException(nameof(requestParams));
        }

        if (Mode == McpTaskMigrationMode.Modern)
        {
            var modernResult = await _client.CallToolAsTaskAsync(requestParams, cancellationToken).ConfigureAwait(false);
            return modernResult.IsTask
                ? new McpTaskMigrationCallResult(Mode, modernResult.TaskCreated!)
                : new McpTaskMigrationCallResult(Mode, modernResult.Result!);
        }

        var legacyResult = await _client.CallToolAsLegacyTaskAsync(requestParams, cancellationToken: cancellationToken).ConfigureAwait(false);
        return legacyResult.IsTask
            ? new McpTaskMigrationCallResult(Mode, legacyResult.Task!)
            : new McpTaskMigrationCallResult(Mode, legacyResult.Result!);
    }

    /// <summary>Calls a tool and waits for a task result when the selected implementation creates a task.</summary>
    public async ValueTask<CallToolResult> CallToolWithPollingAsync(
        CallToolRequestParams requestParams,
        int maxConsecutiveStuckPolls = 60,
        CancellationToken cancellationToken = default)
    {
        if (requestParams is null)
        {
            throw new ArgumentNullException(nameof(requestParams));
        }

        if (Mode == McpTaskMigrationMode.Modern)
        {
            return await _client.CallToolWithPollingAsync(
                requestParams,
                maxConsecutiveStuckPolls,
                cancellationToken).ConfigureAwait(false);
        }

        var legacyResult = await _client.CallToolAsLegacyTaskAsync(requestParams, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!legacyResult.IsTask)
        {
            return legacyResult.Result!;
        }

        JsonElement payload = await _client.GetLegacyTaskPayloadAsync(legacyResult.Task!.TaskId, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(payload, McpJsonUtilities.DefaultOptions.GetTypeInfo<CallToolResult>())
            ?? throw new JsonException("The legacy task result payload was empty.");
    }
}

/// <summary>Provides factory methods for <see cref="McpTaskMigrationClient"/>.</summary>
public static class McpTaskMigrationClientExtensions
{
    /// <summary>
    /// Creates a Tasks migration facade for a connected client.
    /// </summary>
    /// <param name="client">The connected MCP client.</param>
    /// <param name="logger">
    /// An optional logger that records servers for which the legacy Tasks draft was selected.
    /// </param>
    /// <returns>A facade bound to the Tasks implementation for the negotiated protocol version.</returns>
    /// <exception cref="InvalidOperationException">
    /// The client did not negotiate the legacy Tasks protocol or a version supporting the modern Tasks extension.
    /// </exception>
    public static McpTaskMigrationClient CreateTaskMigrationClient(this McpClient client, ILogger? logger = null)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (McpProtocolVersions.IsJuly2026OrLaterProtocolVersion(client.NegotiatedProtocolVersion))
        {
            return new McpTaskMigrationClient(client, McpTaskMigrationMode.Modern);
        }

        if (string.Equals(client.NegotiatedProtocolVersion, LegacyTasksProtocol.ProtocolVersion, StringComparison.Ordinal))
        {
            logger?.LogInformation(
                "Using the legacy MCP Tasks implementation for server {ServerName} {ServerVersion} negotiated at protocol version {ProtocolVersion}.",
                client.ServerInfo.Name,
                client.ServerInfo.Version,
                client.NegotiatedProtocolVersion);
            return new McpTaskMigrationClient(client, McpTaskMigrationMode.Legacy);
        }

        throw new InvalidOperationException(
            $"Tasks migration requires protocol version '{LegacyTasksProtocol.ProtocolVersion}' or " +
            $"'{McpProtocolVersions.July2026ProtocolVersion}' or later, but the connected server negotiated " +
            $"'{client.NegotiatedProtocolVersion ?? "(none)"}'.");
    }
}

/// <summary>Represents either an immediate tool result or a task created by the selected Tasks implementation.</summary>
public sealed class McpTaskMigrationCallResult
{
    internal McpTaskMigrationCallResult(McpTaskMigrationMode mode, CallToolResult result)
    {
        Mode = mode;
        Result = result;
    }

    internal McpTaskMigrationCallResult(McpTaskMigrationMode mode, McpLegacyTask task)
    {
        Mode = mode;
        LegacyTask = task;
    }

    internal McpTaskMigrationCallResult(McpTaskMigrationMode mode, ModelContextProtocol.Extensions.Tasks.CreateTaskResult task)
    {
        Mode = mode;
        ModernTask = task;
    }

    /// <summary>Gets the Tasks implementation that produced this result.</summary>
    public McpTaskMigrationMode Mode { get; }

    /// <summary>Gets whether the server created a task.</summary>
    public bool IsTask => LegacyTask is not null || ModernTask is not null;

    /// <summary>Gets the immediate tool result when <see cref="IsTask"/> is <see langword="false"/>.</summary>
    public CallToolResult? Result { get; }

    /// <summary>Gets the task created by the 2025-11-30 legacy Tasks draft.</summary>
    public McpLegacyTask? LegacyTask { get; }

    /// <summary>Gets the task created by the 2026-07-28 or later Tasks extension.</summary>
    public ModelContextProtocol.Extensions.Tasks.CreateTaskResult? ModernTask { get; }
}
