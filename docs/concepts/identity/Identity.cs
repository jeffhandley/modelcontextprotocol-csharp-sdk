using System.ComponentModel;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Docs.Snippets.Identity;

// <snippet_IdentityDirectInjection>
[McpServerToolType]
public class UserAwareTools
{
    [McpServerTool, Description("Returns a personalized greeting.")]
    public string Greet(ClaimsPrincipal? user, string message)
    {
        var userName = user?.Identity?.Name ?? "anonymous";
        return $"{userName}: {message}";
    }
}
// </snippet_IdentityDirectInjection>

// <snippet_IdentityPrompt>
[McpServerPromptType]
public class UserAwarePrompts
{
    [McpServerPrompt, Description("Creates a user-specific prompt.")]
    public ChatMessage PersonalizedPrompt(ClaimsPrincipal? user, string topic)
    {
        var userName = user?.Identity?.Name ?? "user";
        return new(ChatRole.User, $"As {userName}, explain {topic}.");
    }
}
// </snippet_IdentityPrompt>

// <snippet_IdentityRoleProtected>
[McpServerToolType]
public class RoleProtectedTools
{
    [McpServerTool, Description("Available to all authenticated users.")]
    [Authorize]
    public string GetData(string query)
    {
        return $"Data for: {query}";
    }

    [McpServerTool, Description("Admin-only operation.")]
    [Authorize(Roles = "Admin")]
    public string AdminOperation(string action)
    {
        return $"Admin action: {action}";
    }

    [McpServerTool, Description("Public tool accessible without authentication.")]
    [AllowAnonymous]
    public string PublicInfo()
    {
        return "This is public information.";
    }
}
// </snippet_IdentityRoleProtected>

// <snippet_IdentityHttpContext>
[McpServerToolType]
public class HttpContextTools(IHttpContextAccessor contextAccessor)
{
    [McpServerTool, Description("Returns data filtered by caller identity.")]
    public string GetFilteredData(string query)
    {
        var httpContext = contextAccessor.HttpContext
            ?? throw new InvalidOperationException("No HTTP context available.");
        var userName = httpContext.User.Identity?.Name ?? "anonymous";
        return $"{userName}: results for '{query}'";
    }
}
// </snippet_IdentityHttpContext>

internal static class IdentityFilters
{
    public static void ConfigureRequestFilters(IServiceCollection services)
    {
        // <snippet_IdentityRequestFilter>
        services.AddMcpServer()
            .WithRequestFilters(requestFilters =>
            {
                requestFilters.AddCallToolFilter(next => async (context, cancellationToken) =>
                {
                    // Access user identity in a filter
                    var userName = context.User?.Identity?.Name;
                    var logger = context.Services?.GetService<ILogger<Program>>();
                    logger?.LogInformation("Tool called by: {User}", userName ?? "anonymous");

                    return await next(context, cancellationToken);
                });
            })
            .WithTools<UserAwareTools>();
        // </snippet_IdentityRequestFilter>
    }

    public static void ConfigureAuthorization(IServiceCollection services)
    {
        // <snippet_IdentityAuthzSetup>
        services.AddMcpServer()
            .WithHttpTransport()
            .AddAuthorizationFilters()
            .WithTools<RoleProtectedTools>();
        // </snippet_IdentityAuthzSetup>
    }

    public static void ConfigureStdioIdentity(IServiceCollection services)
    {
        // <snippet_IdentityStdioFilter>
        services.AddMcpServer()
            .WithMessageFilters(messageFilters =>
            {
                messageFilters.AddIncomingFilter(next => async (context, cancellationToken) =>
                {
                    // Set user based on process-level context
                    var role = Environment.GetEnvironmentVariable("MCP_USER_ROLE") ?? "default";
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.Name, "stdio-user"), new Claim(ClaimTypes.Role, role)],
                        "StdioAuth", ClaimTypes.Name, ClaimTypes.Role));

                    await next(context, cancellationToken);
                });
            })
            .WithTools<UserAwareTools>();
        // </snippet_IdentityStdioFilter>
    }
}

internal sealed class Program;
