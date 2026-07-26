using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

// This file documents capabilities that are deprecated per SEP-2577 (Roots, Sampling, Logging).
// Suppress the spec-deprecation advisory so the illustrative snippets still compile; the pragma
// sits outside every snippet region and is not rendered in the docs.
#pragma warning disable MCP9005

namespace Docs.Snippets.Capabilities;

internal static class ClientCapabilitiesConfig
{
    public static async Task Configure(IClientTransport transport)
    {
        // <snippet_ClientCapabilities>
        var options = new McpClientOptions
        {
            Capabilities = new ClientCapabilities
            {
                Roots = new RootsCapability { ListChanged = true },
                Sampling = new SamplingCapability(),
                Elicitation = new ElicitationCapability
                {
                    Form = new FormElicitationCapability(),
                    Url = new UrlElicitationCapability()
                }
            }
        };

        await using var client = await McpClient.CreateAsync(transport, options);
        // </snippet_ClientCapabilities>
    }
}

internal static class ServerCapabilityChecks
{
    public static async Task Check(IClientTransport transport)
    {
        // <snippet_CheckServerCapabilities>
        await using var client = await McpClient.CreateAsync(transport);

        // Check if the server supports tools
        if (client.ServerCapabilities.Tools is not null)
        {
            var tools = await client.ListToolsAsync();
        }

        // Check if the server supports resources with subscriptions
        if (client.ServerCapabilities.Resources is { Subscribe: true })
        {
            await client.SubscribeToResourceAsync("config://app/settings");
        }

        // Check if the server supports prompts with list-changed notifications
        if (client.ServerCapabilities.Prompts is { ListChanged: true })
        {
            client.RegisterNotificationHandler(
                NotificationMethods.PromptListChangedNotification,
                async (notification, ct) =>
                {
                    var prompts = await client.ListPromptsAsync(cancellationToken: ct);
                });
        }

        // Check if the server supports logging
        if (client.ServerCapabilities.Logging is not null)
        {
            await client.SetLoggingLevelAsync(LoggingLevel.Info);
        }

        // Check if the server supports completions
        if (client.ServerCapabilities.Completions is not null)
        {
            var completions = await client.CompleteAsync(
                new PromptReference { Name = "my_prompt" },
                argumentName: "language",
                argumentValue: "py");
        }
        // </snippet_CheckServerCapabilities>
    }
}
