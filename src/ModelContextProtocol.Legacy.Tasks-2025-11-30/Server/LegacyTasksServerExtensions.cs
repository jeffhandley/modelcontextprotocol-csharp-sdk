using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ModelContextProtocol.Legacy.Tasks;

/// <summary>Provides server APIs for the 2025-11-30 Tasks draft.</summary>
public static class LegacyTasksServerExtensions
{
    /// <summary>Sends an optional legacy task-status notification.</summary>
    public static Task SendLegacyTaskStatusNotificationAsync(
        this McpServer server,
        LegacyTaskStatusNotificationParams notificationParams,
        CancellationToken cancellationToken = default)
    {
        if (server is null)
        {
            throw new ArgumentNullException(nameof(server));
        }

        if (notificationParams is null)
        {
            throw new ArgumentNullException(nameof(notificationParams));
        }

        return server.SendNotificationAsync(
            LegacyTasksProtocol.TaskStatusNotificationMethod,
            notificationParams,
            LegacyTasksJsonContext.Default.Options,
            cancellationToken);
    }
}
