// Snippets for docs/concepts/transports/transports.md

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Docs.Snippets.Transports;

[McpServerToolType]
public class MyTools
{
    [McpServerTool]
    public static string Echo(string message) => message;
}

internal static class TransportsSnippets
{
    public static async Task StdioClient()
    {
        // <snippet_TransportsStdioClient>
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Command = "dnx",
            Arguments = ["NuGet.Mcp.Server"],
            ShutdownTimeout = TimeSpan.FromSeconds(10)
        });

        await using var client = await McpClient.CreateAsync(transport);
        // </snippet_TransportsStdioClient>
    }

    public static void StdioEnvDefault()
    {
        // <snippet_TransportsStdioEnvDefault>
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Command = "my-mcp-server",
            InheritEnvironmentVariables = false,
            EnvironmentVariables = StdioClientTransportOptions.GetDefaultEnvironmentVariables(),
        });
        // </snippet_TransportsStdioEnvDefault>
    }

    public static void StdioEnvAdd(string apiKey)
    {
        // <snippet_TransportsStdioEnvAdd>
        var env = StdioClientTransportOptions.GetDefaultEnvironmentVariables();
        env["MY_SERVER_API_KEY"] = apiKey;

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Command = "my-mcp-server",
            InheritEnvironmentVariables = false,
            EnvironmentVariables = env,
        });
        // </snippet_TransportsStdioEnvAdd>
    }

    public static void StdioEnvManual()
    {
        // <snippet_TransportsStdioEnvManual>
        var env = new Dictionary<string, string?>();
        foreach (var name in new[] { "PATH", "HOME", "HTTP_PROXY", "HTTPS_PROXY" })
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (value is not null)
                env[name] = value;
        }

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Command = "my-mcp-server",
            InheritEnvironmentVariables = false,
            EnvironmentVariables = env,
        });
        // </snippet_TransportsStdioEnvManual>
    }

    public static async Task StdioServer(string[] args)
    {
        // <snippet_TransportsStdioServer>
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<MyTools>();

        await builder.Build().RunAsync();
        // </snippet_TransportsStdioServer>
    }

    public static async Task HttpClientSnippet()
    {
        // <snippet_TransportsHttpClient>
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri("https://my-mcp-server.example.com/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp,
            ConnectionTimeout = TimeSpan.FromSeconds(30),
            AdditionalHeaders = new Dictionary<string, string>
            {
                ["X-Custom-Header"] = "value"
            }
        });

        await using var client = await McpClient.CreateAsync(transport);
        // </snippet_TransportsHttpClient>
    }

    public static void HttpAutoDetect()
    {
        // <snippet_TransportsHttpAutoDetect>
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri("https://my-mcp-server.example.com/mcp"),
            // TransportMode defaults to AutoDetect
        });
        // </snippet_TransportsHttpAutoDetect>
    }

    public static async Task Resume()
    {
        string? previousSessionId = null;
        ServerCapabilities previousServerCapabilities = new();
        Implementation previousServerInfo = new() { Name = "my-mcp-server", Version = "1.0.0" };

        // <snippet_TransportsResume>
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri("https://my-mcp-server.example.com/mcp"),
            KnownSessionId = previousSessionId
        });

        await using var client = await McpClient.ResumeSessionAsync(transport, new ResumeClientSessionOptions
        {
            ServerCapabilities = previousServerCapabilities,
            ServerInfo = previousServerInfo
        });
        // </snippet_TransportsResume>
    }

    public static void HttpServer(string[] args)
    {
        // <snippet_TransportsHttpServer>
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddMcpServer()
            .WithHttpTransport(options =>
            {
                // Recommended for servers that don't need server-to-client requests.
                options.Stateless = true;
            })
            .WithTools<MyTools>();

        var app = builder.Build();
        app.MapMcp();
        app.Run();
        // </snippet_TransportsHttpServer>
    }

    public static void Cors(WebApplicationBuilder builder)
    {
        // <snippet_TransportsCors>
        var allowedOrigins = builder.Configuration.GetSection("Mcp:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"];

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("McpBrowserClient", policy =>
            {
                policy.WithOrigins(allowedOrigins)
                    // Add `GET` for standalone/resumable SSE streams and DELETE for stateful session termination.
                    .WithMethods("POST", "GET", "DELETE")
                    .WithHeaders("Content-Type", "Authorization", "MCP-Protocol-Version", "Mcp-Session-Id")
                    .WithExposedHeaders("Mcp-Session-Id");
            });
        });

        var app = builder.Build();

        app.UseCors();
        app.MapMcp("/mcp").RequireCors("McpBrowserClient");
        // </snippet_TransportsCors>
    }

    public static void CustomRoute(WebApplication app)
    {
        // <snippet_TransportsCustomRoute>
        app.MapMcp("/mcp");
        // </snippet_TransportsCustomRoute>
    }

    public static async Task SseClient()
    {
        // <snippet_TransportsSseClient>
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri("https://my-mcp-server.example.com/sse"),
            TransportMode = HttpTransportMode.Sse,
            MaxReconnectionAttempts = 5,
            DefaultReconnectionInterval = TimeSpan.FromSeconds(1)
        });

        await using var client = await McpClient.CreateAsync(transport);
        // </snippet_TransportsSseClient>
    }

    public static void SseServer(string[] args)
    {
        // <snippet_TransportsSseServer>
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddMcpServer()
            .WithHttpTransport(options =>
            {
                // SSE requires stateful mode (the default). Set explicitly for forward compatibility.
                options.Stateless = false;

#pragma warning disable MCP9004 // EnableLegacySse is obsolete
                // Enable legacy SSE endpoints for clients that don't support Streamable HTTP.
                // See sessions doc for backpressure implications.
                options.EnableLegacySse = true;
#pragma warning restore MCP9004
            })
            .WithTools<MyTools>();

        var app = builder.Build();

        // MapMcp() serves Streamable HTTP. Legacy SSE (/sse and /message) is also
        // available because EnableLegacySse is set to true above.
        app.MapMcp();
        app.Run();
        // </snippet_TransportsSseServer>
    }
}
