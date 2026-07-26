using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

// Async lambdas without an await (e.g. base handlers that return synchronously) are
// intentional in these illustrative snippets, as are the unboxing/reference casts that
// the doc code performs on values it just placed (Items and cache entries).
#pragma warning disable CS1998 // async method lacks await
#pragma warning disable CS8600 // converting null literal or possible null to non-nullable
#pragma warning disable CS8603 // possible null reference return
#pragma warning disable CS8605 // unboxing a possibly null value

namespace Docs.Snippets.Filters;

// Referenced by ILogger<Program> in the filter snippets below.
internal sealed class Program;

[McpServerToolType]
internal sealed class MyTools
{
    [McpServerTool, Description("An example tool")]
    public static string Example() => "example";
}

// <snippet_FiltersWeatherTools>
[McpServerToolType]
public class WeatherTools
{
    [McpServerTool, Description("Gets public weather data")]
    public static string GetWeather(string location)
    {
        return $"Weather for {location}: Sunny, 25°C";
    }

    [McpServerTool, Description("Gets detailed weather forecast")]
    [Authorize] // Requires authentication
    public static string GetDetailedForecast(string location)
    {
        return $"Detailed forecast for {location}: ...";
    }

    [McpServerTool, Description("Manages weather alerts")]
    [Authorize(Roles = "Admin")] // Requires Admin role
    public static string ManageWeatherAlerts(string alertType)
    {
        return $"Managing alert: {alertType}";
    }
}
// </snippet_FiltersWeatherTools>

// <snippet_FiltersClassAuth>
[McpServerToolType]
[Authorize] // All tools require authentication
public class RestrictedTools
{
    [McpServerTool, Description("Restricted tool accessible to authenticated users")]
    public static string RestrictedOperation()
    {
        return "Restricted operation completed";
    }

    [McpServerTool, Description("Public tool accessible to anonymous users")]
    [AllowAnonymous] // Overrides class-level [Authorize]
    public static string PublicOperation()
    {
        return "Public operation completed";
    }
}
// </snippet_FiltersClassAuth>

internal static class FilterSnippets
{
    static IList<Tool> GetTools() => [];

    public static void Incoming(IServiceCollection services)
    {
        // <snippet_FiltersIncoming>
        services.AddMcpServer()
            .WithMessageFilters(messageFilters =>
            {
                messageFilters.AddIncomingFilter(next => async (context, cancellationToken) =>
                {
                    var logger = context.Services?.GetService<ILogger<Program>>();

                    // Access the raw JSON-RPC message
                    if (context.JsonRpcMessage is JsonRpcRequest request)
                    {
                        logger?.LogInformation($"Incoming request: {request.Method}");
                    }

                    // Call next to continue processing
                    await next(context, cancellationToken);
                });
            })
            .WithTools<MyTools>();
        // </snippet_FiltersIncoming>
    }

    public static void SkipDefault(IServiceCollection services)
    {
        services.AddMcpServer()
        // <snippet_FiltersSkipDefault>
        .WithMessageFilters(messageFilters =>
        {
            messageFilters.AddIncomingFilter(next => async (context, cancellationToken) =>
            {
                if (context.JsonRpcMessage is JsonRpcRequest request && request.Method == "custom/myMethod")
                {
                    // Handle the custom method directly
                    var response = new JsonRpcResponse
                    {
                        Id = request.Id,
                        Result = JsonSerializer.SerializeToNode(new { message = "Custom response" })
                    };
                    await context.Server.SendMessageAsync(response, cancellationToken);
                    return; // Don't call next - we handled it
                }

                await next(context, cancellationToken);
            });
        })
        // </snippet_FiltersSkipDefault>
        .WithTools<MyTools>();
    }

    public static void Outgoing(IServiceCollection services)
    {
        // <snippet_FiltersOutgoing>
        services.AddMcpServer()
            .WithMessageFilters(messageFilters =>
            {
                messageFilters.AddOutgoingFilter(next => async (context, cancellationToken) =>
                {
                    var logger = context.Services?.GetService<ILogger<Program>>();

                    // Inspect outgoing messages
                    switch (context.JsonRpcMessage)
                    {
                        case JsonRpcResponse response:
                            logger?.LogInformation($"Sending response for request {response.Id}");
                            break;
                        case JsonRpcNotification notification:
                            logger?.LogInformation($"Sending notification: {notification.Method}");
                            break;
                    }

                    await next(context, cancellationToken);
                });
            })
            .WithTools<MyTools>();
        // </snippet_FiltersOutgoing>
    }

    public static void SkipOutgoing(IServiceCollection services)
    {
        services.AddMcpServer()
        // <snippet_FiltersSkipOutgoing>
        .WithMessageFilters(messageFilters =>
        {
            messageFilters.AddOutgoingFilter(next => async (context, cancellationToken) =>
            {
                // Suppress specific notifications
                if (context.JsonRpcMessage is JsonRpcNotification notification &&
                    notification.Method == "notifications/progress")
                {
                    return; // Don't send this notification
                }

                await next(context, cancellationToken);
            });
        })
        // </snippet_FiltersSkipOutgoing>
        .WithTools<MyTools>();
    }

    public static void SendAdditional(IServiceCollection services)
    {
        services.AddMcpServer()
        // <snippet_FiltersSendAdditional>
        .WithMessageFilters(messageFilters =>
        {
            messageFilters.AddOutgoingFilter(next => async (context, cancellationToken) =>
            {
                // Send an extra notification before certain responses
                if (context.JsonRpcMessage is JsonRpcResponse response &&
                    response.Result is JsonObject result &&
                    result.ContainsKey("tools"))
                {
                    var notification = new JsonRpcNotification
                    {
                        Method = "custom/toolsListed",
                        Params = new JsonObject { ["timestamp"] = DateTime.UtcNow.ToString("O") },
                        Context = new JsonRpcMessageContext
                        {
                            RelatedTransport = context.JsonRpcMessage.Context?.RelatedTransport
                        }
                    };
                    await next(new MessageContext(context.Server, notification), cancellationToken);
                }

                await next(context, cancellationToken);
            });
        })
        // </snippet_FiltersSendAdditional>
        .WithTools<MyTools>();
    }

    public static void Order(IServiceCollection services)
    {
        McpMessageFilter incomingFilter1 = next => next;
        McpMessageFilter incomingFilter2 = next => next;
        McpMessageFilter outgoingFilter1 = next => next;
        McpMessageFilter outgoingFilter2 = next => next;
        McpRequestFilter<ListToolsRequestParams, ListToolsResult> toolsFilter = next => next;

        // <snippet_FiltersOrder>
        services.AddMcpServer()
            .WithMessageFilters(messageFilters =>
            {
                messageFilters.AddIncomingFilter(incomingFilter1); // Incoming: executes first (outermost)
                messageFilters.AddIncomingFilter(incomingFilter2); // Incoming: executes second
                messageFilters.AddOutgoingFilter(outgoingFilter1); // Outgoing: executes first (outermost)
                messageFilters.AddOutgoingFilter(outgoingFilter2); // Outgoing: executes second
            })
            .WithRequestFilters(requestFilters =>
            {
                requestFilters.AddListToolsFilter(toolsFilter);    // Request-specific filter
            })
            .WithTools<MyTools>();
        // </snippet_FiltersOrder>
    }

    public static void PassingData(IServiceCollection services)
    {
        services.AddMcpServer()
        // <snippet_FiltersPassingData>
        .WithMessageFilters(messageFilters =>
        {
            messageFilters.AddIncomingFilter(next => async (context, cancellationToken) =>
            {
                context.Items["requestStartTime"] = DateTime.UtcNow;
                await next(context, cancellationToken);
            });

            messageFilters.AddIncomingFilter(next => async (context, cancellationToken) =>
            {
                await next(context, cancellationToken);

                if (context.Items.TryGetValue("requestStartTime", out var startTime))
                {
                    var elapsed = DateTime.UtcNow - (DateTime)startTime;
                    var logger = context.Services?.GetService<ILogger<Program>>();
                    logger?.LogInformation($"Request processed in {elapsed.TotalMilliseconds}ms");
                }
            });
        })
        // </snippet_FiltersPassingData>
        .WithTools<MyTools>();
    }

    public static void Usage(IServiceCollection services)
    {
        // <snippet_FiltersUsage>
        services.AddMcpServer()
            .WithListToolsHandler(async (context, cancellationToken) =>
            {
                // Your base handler logic
                return new ListToolsResult { Tools = GetTools() };
            })
            .WithRequestFilters(requestFilters =>
            {
                requestFilters.AddListToolsFilter(next => async (context, cancellationToken) =>
                {
                    var logger = context.Services?.GetService<ILogger<Program>>();

                    // Pre-processing logic
                    logger?.LogInformation("Before handler execution");

                    var result = await next(context, cancellationToken);

                    // Post-processing logic
                    logger?.LogInformation("After handler execution");
                    return result;
                });
            });
        // </snippet_FiltersUsage>
    }

    public static void RequestOrder(IServiceCollection services)
    {
        McpRequestHandler<ListToolsRequestParams, ListToolsResult> baseHandler = (context, ct) => default;
        McpRequestFilter<ListToolsRequestParams, ListToolsResult> filter1 = next => next;
        McpRequestFilter<ListToolsRequestParams, ListToolsResult> filter2 = next => next;
        McpRequestFilter<ListToolsRequestParams, ListToolsResult> filter3 = next => next;

        // <snippet_FiltersRequestOrder>
        services.AddMcpServer()
            .WithListToolsHandler(baseHandler)
            .WithRequestFilters(requestFilters =>
            {
                requestFilters.AddListToolsFilter(filter1); // Executes first (outermost)
                requestFilters.AddListToolsFilter(filter2); // Executes second
                requestFilters.AddListToolsFilter(filter3); // Executes third (closest to handler)
            });
        // </snippet_FiltersRequestOrder>
    }

    public static void Logging(IServiceCollection services)
    {
        services.AddMcpServer()
        // <snippet_FiltersLogging>
        .WithRequestFilters(requestFilters =>
        {
            requestFilters.AddListToolsFilter(next => async (context, cancellationToken) =>
            {
                var logger = context.Services?.GetService<ILogger<Program>>();

                logger?.LogInformation($"Processing request from {context.Params.ProgressToken}");
                var result = await next(context, cancellationToken);
                logger?.LogInformation($"Returning {result.Tools?.Count ?? 0} tools");
                return result;
            });
        })
        // </snippet_FiltersLogging>
        .WithTools<MyTools>();
    }

    public static void ErrorHandling(IServiceCollection services)
    {
        services.AddMcpServer()
        // <snippet_FiltersErrorHandling>
        .WithRequestFilters(requestFilters =>
        {
            requestFilters.AddCallToolFilter(next => async (context, cancellationToken) =>
            {
                try
                {
                    return await next(context, cancellationToken);
                }
                catch (Exception ex)
                {
                    var logger = context.Services?.GetService<ILogger<Program>>();
                    logger?.LogError(ex, "Error while processing CallTool request for {ProgressToken}", context.Params.ProgressToken);

                    return new CallToolResult
                    {
                        Content = [new TextContentBlock { Text = "An unexpected error occurred while processing the tool call." }],
                        IsError = true
                    };
                }
            });
        })
        // </snippet_FiltersErrorHandling>
        .WithTools<MyTools>();
    }

    public static void Performance(IServiceCollection services)
    {
        services.AddMcpServer()
        // <snippet_FiltersPerformance>
        .WithRequestFilters(requestFilters =>
        {
            requestFilters.AddListToolsFilter(next => async (context, cancellationToken) =>
            {
                var logger = context.Services?.GetService<ILogger<Program>>();

                var stopwatch = Stopwatch.StartNew();
                var result = await next(context, cancellationToken);
                stopwatch.Stop();
                logger?.LogInformation($"Handler took {stopwatch.ElapsedMilliseconds}ms");
                return result;
            });
        })
        // </snippet_FiltersPerformance>
        .WithTools<MyTools>();
    }

    public static void Caching(IServiceCollection services)
    {
        services.AddMcpServer()
        // <snippet_FiltersCaching>
        .WithRequestFilters(requestFilters =>
        {
            requestFilters.AddListResourcesFilter(next => async (context, cancellationToken) =>
            {
                var cache = context.Services!.GetRequiredService<IMemoryCache>();

                var cacheKey = $"resources:{context.Params.Cursor}";
                if (cache.TryGetValue(cacheKey, out var cached))
                {
                    return (ListResourcesResult)cached;
                }

                var result = await next(context, cancellationToken);
                cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
                return result;
            });
        })
        // </snippet_FiltersCaching>
        .WithTools<MyTools>();
    }

    public static void AuthEnable(IServiceCollection services)
    {
        // <snippet_FiltersAuthEnable>
        services.AddMcpServer()
            .WithHttpTransport(o => o.Stateless = true)
            .AddAuthorizationFilters() // Enable authorization filter support
            .WithTools<WeatherTools>();
        // </snippet_FiltersAuthEnable>
    }

    public static void AuthOrder(IServiceCollection services)
    {
        // <snippet_FiltersAuthOrder>
        services.AddMcpServer()
            .WithHttpTransport(o => o.Stateless = true)
            .WithRequestFilters(requestFilters =>
            {
                requestFilters.AddListToolsFilter(next => async (context, cancellationToken) =>
                {
                    var logger = context.Services?.GetService<ILogger<Program>>();

                    // This filter runs BEFORE authorization - sees all tools
                    logger?.LogInformation("Request for tools list - will see all tools");
                    var result = await next(context, cancellationToken);
                    logger?.LogInformation($"Returning {result.Tools?.Count ?? 0} tools after authorization");
                    return result;
                });
            })
            .AddAuthorizationFilters() // Authorization filtering happens here
            .WithRequestFilters(requestFilters =>
            {
                requestFilters.AddListToolsFilter(next => async (context, cancellationToken) =>
                {
                    var logger = context.Services?.GetService<ILogger<Program>>();

                    // This filter runs AFTER authorization - only sees authorized tools
                    var result = await next(context, cancellationToken);
                    logger?.LogInformation($"Post-auth filter sees {result.Tools?.Count ?? 0} authorized tools");
                    return result;
                });
            })
            .WithTools<WeatherTools>();
        // </snippet_FiltersAuthOrder>
    }

    public static void Setup(string[] args)
    {
        // <snippet_FiltersSetup>
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddAuthentication("Bearer")
            .AddJwtBearer(options => { /* JWT configuration */ })
            .AddMcp(options => { /* Resource metadata configuration */ });
        builder.Services.AddAuthorization();

        builder.Services.AddMcpServer()
            .WithHttpTransport(options =>
            {
                options.Stateless = true;
            })
            .AddAuthorizationFilters() // Required for authorization support
            .WithTools<WeatherTools>()
            .WithRequestFilters(requestFilters =>
            {
                requestFilters.AddCallToolFilter(next => async (context, cancellationToken) =>
                {
                    // Custom call tool logic
                    return await next(context, cancellationToken);
                });
            });

        var app = builder.Build();

        app.MapMcp();
        app.Run();
        // </snippet_FiltersSetup>
    }

    public static void CustomAuth(IServiceCollection services)
    {
        services.AddMcpServer()
        // <snippet_FiltersCustomAuth>
        .WithRequestFilters(requestFilters =>
        {
            requestFilters.AddCallToolFilter(next => async (context, cancellationToken) =>
            {
                // Custom authorization logic
                if (context.User?.Identity?.IsAuthenticated != true)
                {
                    return new CallToolResult
                    {
                        Content = [new TextContentBlock { Text = "Custom: Authentication required" }],
                        IsError = true
                    };
                }

                return await next(context, cancellationToken);
            });
        })
        // </snippet_FiltersCustomAuth>
        .WithTools<MyTools>();
    }
}
