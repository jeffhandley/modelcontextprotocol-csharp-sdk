using System.Diagnostics;
using System.IO.Pipelines;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

#pragma warning disable MCP9006 // Stateful-only options are obsolete; shown here for the configuration reference.
#pragma warning disable CS1998 // Doc sample async lambdas intentionally omit awaits.

namespace Docs.Snippets.Stateless;

internal static class StatelessSnippets
{
    private static readonly McpServerTool[] adminTools = [];

    public static void ProtocolVersionExample()
    {
        // <snippet_StatelessProtocolVersion>
        var clientOptions = new McpClientOptions
        {
            ProtocolVersion = "2026-07-28",
        };
        // </snippet_StatelessProtocolVersion>
        _ = clientOptions;
    }

    public static void EnableStateless(string[] args)
    {
        // <snippet_StatelessEnabling>
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddMcpServer()
            .WithHttpTransport(options =>
            {
                options.Stateless = true;
            })
            .WithTools<MyTools>();

        var app = builder.Build();
        app.MapMcp();
        app.Run();
        // </snippet_StatelessEnabling>
    }

    public static void ConfigReference(WebApplicationBuilder builder)
    {
        // <snippet_StatelessConfigReference>
        builder.Services.AddMcpServer()
            .WithHttpTransport(options =>
            {
                // Recommended for servers that don't need sessions.
                options.Stateless = true;

                // --- Options below only apply to stateful (non-stateless) mode ---

                // How long a session can be idle before being closed (default: 2 hours)
                options.IdleTimeout = TimeSpan.FromMinutes(30);

                // Maximum number of idle sessions in memory (default: 10,000)
                options.MaxIdleSessionCount = 1_000;

                // Customize McpServerOptions per session with access to HttpContext
                options.ConfigureSessionOptions = async (httpContext, mcpServerOptions, cancellationToken) =>
                {
                    // Example: customize tools based on the authenticated user's roles
                    var user = httpContext.User;
                    if (user.IsInRole("admin"))
                    {
                        mcpServerOptions.ToolCollection = [.. adminTools];
                    }
                };
            });
        // </snippet_StatelessConfigReference>
    }

    public static void ConfigureSessionExample(HttpServerTransportOptions options)
    {
        // <snippet_StatelessConfigureSession>
        options.ConfigureSessionOptions = async (httpContext, mcpServerOptions, cancellationToken) =>
        {
            // Filter available tools based on a route parameter
            var category = httpContext.Request.RouteValues["category"]?.ToString() ?? "all";
            mcpServerOptions.ToolCollection = GetToolsForCategory(category);

            // Set server info based on the authenticated user
            var userName = httpContext.User.Identity?.Name;
            mcpServerOptions.ServerInfo = new() { Name = $"MCP Server ({userName})", Version = "1.0.0" };
        };
        // </snippet_StatelessConfigureSession>
    }

    public static void PerRequestConfig(WebApplicationBuilder builder)
    {
        // <snippet_StatelessPerRequest>
        builder.Services.AddMcpServer()
            .WithHttpTransport(options =>
            {
                options.Stateless = true;
                options.ConfigureSessionOptions = (httpContext, mcpServerOptions, cancellationToken) =>
                {
                    // This runs on every request in stateless mode, so you can use the
                    // current HttpContext to customize tools, prompts, or resources.
                    var apiVersion = httpContext.Request.Headers["X-Api-Version"].ToString();
                    mcpServerOptions.ToolCollection = GetToolsForVersion(apiVersion);
                    return Task.CompletedTask;
                };
            })
            .WithTools<DefaultTools>();
        // </snippet_StatelessPerRequest>
    }

    public static async Task InMemoryServer(IServiceProvider serviceProvider)
    {
        // <snippet_StatelessInMemory>
        Pipe clientToServerPipe = new(), serverToClientPipe = new();

        await using var scope = serviceProvider.CreateAsyncScope();

        await using McpServer server = McpServer.Create(
            new StreamServerTransport(clientToServerPipe.Reader.AsStream(), serverToClientPipe.Writer.AsStream()),
            new McpServerOptions
            {
                ScopeRequests = false, // The scope is already managed externally.
                ToolCollection = [McpServerTool.Create((string arg) => $"Echo: {arg}", new() { Name = "Echo" })]
            },
            serviceProvider: scope.ServiceProvider);
        // </snippet_StatelessInMemory>
    }

    public static void EndpointFilter(WebApplication app)
    {
        // <snippet_StatelessEndpointFilter>
        app.MapMcp().AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;

            // The session ID is available in the request header on all non-initialize requests
            // in stateful mode (the client echoes back the ID it received from the server's
            // initialize response). It is null for the first initialize request and always null
            // in stateless mode. Tag before next() so child spans inherit the value.
            string? sessionId = httpContext.Request.Headers["Mcp-Session-Id"];
            if (sessionId != null)
            {
                Activity.Current?.AddTag("mcp.transport.session.id", sessionId);
            }

            return await next(context);
        });
        // </snippet_StatelessEndpointFilter>
    }

    public static void SessionMigrationConfig(WebApplicationBuilder builder)
    {
        // <snippet_StatelessSessionMigration>
        builder.Services.AddMcpServer()
            .WithHttpTransport(options =>
            {
                // Session migration is a stateful-mode feature.
                options.Stateless = false;
                options.SessionMigrationHandler = new MySessionMigrationHandler();
            });
        // </snippet_StatelessSessionMigration>
    }

    public static void RegisterMigrationHandler(WebApplicationBuilder builder)
    {
        // <snippet_StatelessMigrationDi>
        builder.Services.AddSingleton<ISessionMigrationHandler, MySessionMigrationHandler>();
        // </snippet_StatelessMigrationDi>
    }

    public static void ResumabilityConfig(WebApplicationBuilder builder)
    {
        // <snippet_StatelessResumability>
        builder.Services.AddMcpServer()
            .WithHttpTransport(options =>
            {
                // Session resumability is a stateful-mode feature.
                options.Stateless = false;
                options.EventStreamStore = new MyEventStreamStore();
            });
        // </snippet_StatelessResumability>
    }

    private static McpServerPrimitiveCollection<McpServerTool>? GetToolsForCategory(string category) => null;
    private static McpServerPrimitiveCollection<McpServerTool>? GetToolsForVersion(string version) => null;
}

[McpServerToolType]
internal class MyTools
{
    [McpServerTool]
    public static string Noop() => "";
}

[McpServerToolType]
internal class DefaultTools
{
    [McpServerTool]
    public static string Noop() => "";
}

internal sealed class MySessionMigrationHandler : ISessionMigrationHandler
{
    public ValueTask OnSessionInitializedAsync(HttpContext context, string sessionId, InitializeRequestParams initializeParams, CancellationToken cancellationToken)
        => default;

    public ValueTask<InitializeRequestParams?> AllowSessionMigrationAsync(HttpContext context, string sessionId, CancellationToken cancellationToken)
        => default;
}

internal sealed class MyEventStreamStore : ISseEventStreamStore
{
    public ValueTask<ISseEventStreamWriter> CreateStreamAsync(SseEventStreamOptions options, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public ValueTask<ISseEventStreamReader?> GetStreamReaderAsync(string lastEventId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
