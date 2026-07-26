// Snippets for docs/concepts/resources/resources.md
// CS1998: some illustrative async handler bodies intentionally omit awaits.
// CS8604: the subscription-tracking example indexes a dictionary with the nullable SessionId.
#pragma warning disable CS1998
#pragma warning disable CS8604

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Docs.Snippets.Resources.ResourceStubs;

namespace Docs.Snippets.Resources;

// <snippet_ResourcesDirect>
[McpServerResourceType]
public class MyResources
{
    [McpServerResource(UriTemplate = "config://app/settings", Name = "App Settings", MimeType = "application/json")]
    [Description("Returns application configuration settings")]
    public static string GetSettings() => JsonSerializer.Serialize(new { theme = "dark", language = "en" });
}
// </snippet_ResourcesDirect>

// <snippet_ResourcesTemplate>
[McpServerResourceType]
public class DocumentResources
{
    [McpServerResource(UriTemplate = "docs://articles/{id}", Name = "Article")]
    [Description("Returns an article by its ID")]
    public static ResourceContents GetArticle(string id)
    {
        string? content = LoadArticle(id); // application logic to load by ID

        if (content is null)
        {
            throw new McpException($"Article not found: {id}");
        }

        return new TextResourceContents
        {
            Uri = $"docs://articles/{id}",
            MimeType = "text/plain",
            Text = content
        };
    }
}
// </snippet_ResourcesTemplate>

[McpServerResourceType]
public class ResourcesTextBinary
{
    // <snippet_ResourcesText>
    [McpServerResource(UriTemplate = "notes://daily/{date}", Name = "Daily Notes")]
    [Description("Returns notes for a given date")]
    public static TextResourceContents GetDailyNotes(string date)
    {
        return new TextResourceContents
        {
            Uri = $"notes://daily/{date}",
            MimeType = "text/markdown",
            Text = $"# Notes for {date}\n\n- Meeting at 10am\n- Review PRs"
        };
    }
    // </snippet_ResourcesText>

    // <snippet_ResourcesBinary>
    [McpServerResource(UriTemplate = "images://photos/{id}", Name = "Photo")]
    [Description("Returns a photo by ID")]
    public static BlobResourceContents GetPhoto(int id)
    {
        byte[] imageData = LoadPhoto(id);
        return BlobResourceContents.FromBytes(imageData, $"images://photos/{id}", "image/png");
    }
    // </snippet_ResourcesBinary>
}

internal static class ResourcesSnippets
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> subscriptions = new();

    public static void Register(WebApplicationBuilder builder)
    {
        // <snippet_ResourcesRegister>
        builder.Services.AddMcpServer()
            .WithHttpTransport(o => o.Stateless = true)
            .WithResources<MyResources>()
            .WithResources<DocumentResources>();
        // </snippet_ResourcesRegister>
    }

    public static async Task ListResources(McpClient client)
    {
        // <snippet_ResourcesList>
        // List direct resources
        IList<McpClientResource> resources = await client.ListResourcesAsync();

        foreach (var resource in resources)
        {
            Console.WriteLine($"{resource.Name} ({resource.Uri})");
            Console.WriteLine($"  MIME: {resource.MimeType}");
            Console.WriteLine($"  Description: {resource.Description}");
        }
        // </snippet_ResourcesList>
    }

    public static async Task ListTemplates(McpClient client)
    {
        // <snippet_ResourcesListTemplates>
        // List resource templates (parameterized URIs)
        IList<McpClientResourceTemplate> templates = await client.ListResourceTemplatesAsync();

        foreach (var template in templates)
        {
            Console.WriteLine($"{template.Name}: {template.UriTemplate}");
        }
        // </snippet_ResourcesListTemplates>
    }

    public static async Task ReadDirect(McpClient client)
    {
        // <snippet_ResourcesRead>
        // Read a direct resource by URI
        ReadResourceResult result = await client.ReadResourceAsync("config://app/settings");

        foreach (var content in result.Contents)
        {
            if (content is TextResourceContents text)
            {
                Console.WriteLine($"[{text.MimeType}] {text.Text}");
            }
            else if (content is BlobResourceContents blob)
            {
                Console.WriteLine($"[{blob.MimeType}] {blob.Blob.Length} bytes");
            }
        }
        // </snippet_ResourcesRead>
    }

    public static async Task ReadTemplate(McpClient client)
    {
        // <snippet_ResourcesReadTemplate>
        // Read a resource using a URI template with parameter values
        ReadResourceResult result = await client.ReadResourceAsync(
            "file:///{path}",
            new Dictionary<string, object?> { ["path"] = "docs/readme.md" });
        // </snippet_ResourcesReadTemplate>
    }

    public static async Task Subscribe(McpClient client)
    {
        // <snippet_ResourcesSubscribe>
        // Subscribe with an inline handler
        IAsyncDisposable subscription = await client.SubscribeToResourceAsync(
            "config://app/settings",
            async (notification, cancellationToken) =>
            {
                Console.WriteLine($"Resource updated: {notification.Uri}");

                // Re-read the resource to get updated content
                var updated = await client.ReadResourceAsync(notification.Uri, cancellationToken: cancellationToken);
                // Process updated content...
            });

        // Later, unsubscribe by disposing
        await subscription.DisposeAsync();
        // </snippet_ResourcesSubscribe>
    }

    public static async Task SubscribeSeparate(McpClient client)
    {
        // <snippet_ResourcesSubscribeSeparate>
        // Subscribe without a handler (use a global notification handler instead)
        await client.SubscribeToResourceAsync("config://app/settings");

        // Unsubscribe when no longer interested
        await client.UnsubscribeFromResourceAsync("config://app/settings");
        // </snippet_ResourcesSubscribeSeparate>
    }

    public static void RegisterHandlers(WebApplicationBuilder builder)
    {
        // <snippet_ResourcesHandlers>
        builder.Services.AddMcpServer()
            // Subscriptions require stateful mode because the server pushes change notifications
            // to clients. Set Stateless = false explicitly for forward compatibility.
            .WithHttpTransport(o => o.Stateless = false)
            .WithResources<MyResources>()
            .WithSubscribeToResourcesHandler(async (ctx, ct) =>
            {
                if (ctx.Params.Uri is { } uri)
                {
                    // Track the subscription (for example, in a concurrent dictionary)
                    subscriptions[ctx.Server.SessionId].TryAdd(uri, 0);
                }
                return new EmptyResult();
            })
            .WithUnsubscribeFromResourcesHandler(async (ctx, ct) =>
            {
                if (ctx.Params.Uri is { } uri)
                {
                    subscriptions[ctx.Server.SessionId].TryRemove(uri, out _);
                }
                return new EmptyResult();
            });
        // </snippet_ResourcesHandlers>
    }

    public static async Task NotifyUpdate(McpServer server)
    {
        // <snippet_ResourcesNotifyUpdate>
        // Notify that a specific resource was updated
        await server.SendNotificationAsync(
            NotificationMethods.ResourceUpdatedNotification,
            new ResourceUpdatedNotificationParams { Uri = "config://app/settings" });
        // </snippet_ResourcesNotifyUpdate>
    }

    public static async Task NotifyList(McpServer server)
    {
        // <snippet_ResourcesNotifyList>
        // After adding or removing resources dynamically
        await server.SendNotificationAsync(
            NotificationMethods.ResourceListChangedNotification,
            new ResourceListChangedNotificationParams());
        // </snippet_ResourcesNotifyList>
    }

    public static void HandleList(McpClient mcpClient)
    {
        // <snippet_ResourcesHandleList>
        mcpClient.RegisterNotificationHandler(
            NotificationMethods.ResourceListChangedNotification,
            async (notification, cancellationToken) =>
            {
                // Refresh the resource list
                var updatedResources = await mcpClient.ListResourcesAsync(cancellationToken: cancellationToken);
                Console.WriteLine($"Resource list updated. {updatedResources.Count} resources available.");
            });
        // </snippet_ResourcesHandleList>
    }
}

internal static class ResourceStubs
{
    public static string? LoadArticle(string id) => "Sample article content.";

    public static byte[] LoadPhoto(int id) => new byte[] { 0x89, 0x50, 0x4E, 0x47 };
}
