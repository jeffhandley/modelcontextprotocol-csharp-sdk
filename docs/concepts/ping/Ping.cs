using ModelContextProtocol.Client;

namespace Docs.Snippets.Ping;

internal static class PingUsage
{
    public static async Task PingServer(McpClient client, CancellationToken cancellationToken)
    {
        // <snippet_Ping>
        await client.PingAsync(cancellationToken: cancellationToken);
        // </snippet_Ping>
    }
}
