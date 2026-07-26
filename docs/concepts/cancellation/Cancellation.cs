using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Docs.Snippets.Cancellation;

[McpServerToolType]
internal static class CancellationTools
{
    // <snippet_LongComputation>
    [McpServerTool, Description("A long-running computation")]
    public static async Task<string> LongComputation(
        [Description("Number of iterations")] int iterations,
        CancellationToken cancellationToken)
    {
        for (int i = 0; i < iterations; i++)
        {
            await Task.Delay(1000, cancellationToken);
        }

        return $"Completed {iterations} iterations.";
    }
    // </snippet_LongComputation>
}

internal static class CancellationObserver
{
    public static void Register(McpClient mcpClient)
    {
        // <snippet_CancellationHandler>
        mcpClient.RegisterNotificationHandler(
            NotificationMethods.CancelledNotification,
            (notification, ct) =>
            {
                var cancelled = notification.Params?.Deserialize<CancelledNotificationParams>(
                    McpJsonUtilities.DefaultOptions);
                if (cancelled is not null)
                {
                    Console.WriteLine($"Request {cancelled.RequestId} cancelled: {cancelled.Reason}");
                }
                return default;
            });
        // </snippet_CancellationHandler>
    }
}
