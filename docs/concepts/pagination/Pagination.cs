using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Docs.Snippets.Pagination;

internal static class AutomaticPagination
{
    public static async Task ListAll(McpClient client)
    {
        // <snippet_AutomaticPagination>
        // Fetches all tools, handling pagination automatically
        IList<McpClientTool> allTools = await client.ListToolsAsync();

        // Fetches all resources, handling pagination automatically
        IList<McpClientResource> allResources = await client.ListResourcesAsync();

        // Fetches all prompts, handling pagination automatically
        IList<McpClientPrompt> allPrompts = await client.ListPromptsAsync();

        // Fetches all resource templates, handling pagination automatically
        IList<McpClientResourceTemplate> allTemplates = await client.ListResourceTemplatesAsync();
        // </snippet_AutomaticPagination>
    }
}

internal static class ManualPagination
{
    public static async Task ListPage(McpClient client)
    {
        // <snippet_ManualPagination>
        string? cursor = null;

        do
        {
            var result = await client.ListToolsAsync(new ListToolsRequestParams
            {
                Cursor = cursor
            });

            // Process this page of results
            foreach (var tool in result.Tools)
            {
                Console.WriteLine($"{tool.Name}: {tool.Description}");
            }

            // Get the cursor for the next page (null when no more pages)
            cursor = result.NextCursor;

        } while (cursor is not null);
        // </snippet_ManualPagination>
    }
}

internal static class ServerPagination
{
    public static void Configure(WebApplicationBuilder builder)
    {
#pragma warning disable CS1998 // Illustrative handler completes synchronously.
        // <snippet_ServerPagination>
        builder.Services.AddMcpServer()
            .WithHttpTransport(o => o.Stateless = true)
            .WithListResourcesHandler(async (ctx, ct) =>
            {
                const int pageSize = 10;
                int startIndex = 0;

                // Parse cursor to determine starting position
                if (ctx.Params.Cursor is { } cursor)
                {
                    startIndex = int.Parse(cursor);
                }

                var allResources = GetAllResources();
                var page = allResources.Skip(startIndex).Take(pageSize).ToList();
                var hasMore = startIndex + pageSize < allResources.Count;

                return new ListResourcesResult
                {
                    Resources = page,
                    NextCursor = hasMore ? (startIndex + pageSize).ToString() : null
                };
            });
        // </snippet_ServerPagination>
#pragma warning restore CS1998
    }

    private static List<Resource> GetAllResources() => new();
}
