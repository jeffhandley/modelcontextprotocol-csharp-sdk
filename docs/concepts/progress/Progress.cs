using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Docs.Snippets.Progress;

internal static class ProgressSnippets
{
    public static async Task ManualHandler(McpClient mcpClient, ProgressToken progressToken)
    {
        // <snippet_ProgressManualHandler>
        await using var handler = mcpClient.RegisterNotificationHandler(NotificationMethods.ProgressNotification,
            (notification, cancellationToken) =>
            {
                if (JsonSerializer.Deserialize<ProgressNotificationParams>(notification.Params) is { } pn &&
                    pn.ProgressToken == progressToken)
                {
                    // progress.Report(pn.Progress);
                    Console.WriteLine($"Tool progress: {pn.Progress.Progress} of {pn.Progress.Total} - {pn.Progress.Message}");
                }
                return ValueTask.CompletedTask;
            });
        // </snippet_ProgressManualHandler>
    }
}
