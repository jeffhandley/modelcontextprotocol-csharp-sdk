using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

// The roots feature is deprecated per SEP-2577; suppress the spec-deprecation advisory so these
// illustrative snippets still compile. The pragma sits outside every snippet region and is not
// rendered in the docs.
#pragma warning disable MCP9005

namespace Docs.Snippets.Roots;

internal static class RootsClient
{
    public static async Task Configure(IClientTransport transport)
    {
        // <snippet_RootsHandler>
        var options = new McpClientOptions
        {
            Handlers = new McpClientHandlers
            {
                RootsHandler = (request, cancellationToken) =>
                {
                    return ValueTask.FromResult(new ListRootsResult
                    {
                        Roots =
                        [
                            new Root
                            {
                                Uri = "file:///home/user/projects/my-app",
                                Name = "My Application"
                            },
                            new Root
                            {
                                Uri = "file:///home/user/projects/shared-lib",
                                Name = "Shared Library"
                            }
                        ]
                    });
                }
            }
        };

        await using var client = await McpClient.CreateAsync(transport, options);
        // </snippet_RootsHandler>
    }

    public static async Task SendChange(McpClient mcpClient)
    {
        // <snippet_RootsListChanged>
        await mcpClient.SendNotificationAsync(
            NotificationMethods.RootsListChangedNotification,
            new RootsListChangedNotificationParams());
        // </snippet_RootsListChanged>
    }

    public static void RegisterHandler(McpServer server)
    {
        // <snippet_RootsChangeHandler>
        server.RegisterNotificationHandler(
            NotificationMethods.RootsListChangedNotification,
            async (notification, cancellationToken) =>
            {
                // Re-request the roots list to get the updated set
                var result = await server.RequestRootsAsync(new ListRootsRequestParams(), cancellationToken);
                Console.WriteLine($"Roots updated. {result.Roots.Count} roots available.");
            });
        // </snippet_RootsChangeHandler>
    }
}

[McpServerToolType]
public static class RootsTools
{
    // <snippet_RequestRoots>
    [McpServerTool, Description("Lists the user's project roots")]
    public static async Task<string> ListProjectRoots(McpServer server, CancellationToken cancellationToken)
    {
        var result = await server.RequestRootsAsync(new ListRootsRequestParams(), cancellationToken);

        var summary = new StringBuilder();
        foreach (var root in result.Roots)
        {
            summary.AppendLine($"- {root.Name ?? root.Uri}: {root.Uri}");
        }

        return summary.ToString();
    }
    // </snippet_RequestRoots>

    // <snippet_RootsMrtr>
    [McpServerTool, Description("Tool that requests roots via MRTR")]
    public static string ListRootsWithMrtr(
        McpServer server,
        RequestContext<CallToolRequestParams> context)
    {
        // On retry, process the client's roots response
        if (context.Params!.InputResponses?.TryGetValue("get_roots", out var response) is true)
        {
            var roots = response.Deserialize(InputResponse.ListRootsResultJsonTypeInfo)?.Roots ?? [];
            return $"Found {roots.Count} roots: {string.Join(", ", roots.Select(r => r.Uri))}";
        }

        if (!server.IsMrtrSupported)
        {
            return "This tool requires MRTR support (2026-07-28, or a stateful session using protocol revision 2025-11-25).";
        }

        // First call — request the client's root list
        throw new InputRequiredException(
            inputRequests: new Dictionary<string, InputRequest>
            {
                ["get_roots"] = InputRequest.ForRootsList(new ListRootsRequestParams())
            },
            requestState: "awaiting-roots");
    }
    // </snippet_RootsMrtr>
}
