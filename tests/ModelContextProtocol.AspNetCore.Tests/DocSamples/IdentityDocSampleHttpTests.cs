// Tests that verify the HTTP-specific code samples from docs/concepts/identity/identity.md compile and function correctly.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore.Tests.Utils;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ModelContextProtocol.Tests.Utils;
using System.ComponentModel;
using System.Security.Claims;

namespace ModelContextProtocol.AspNetCore.Tests.DocSamples;

/// <summary>
/// Validates that the HTTP-specific code samples in the identity documentation compile and produce correct results.
/// Uses the in-memory Kestrel transport pattern for HTTP-based testing.
/// </summary>
public class IdentityDocSampleHttpTests(ITestOutputHelper testOutputHelper) : KestrelInMemoryTest(testOutputHelper)
{
    private async Task<McpClient> ConnectAsync()
    {
        await using var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new("http://localhost:5000"),
        }, HttpClient, LoggerFactory);

        return await McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken, loggerFactory: LoggerFactory);
    }

    private static ClaimsPrincipal CreateUser(string name, params string[] roles)
        => new(new ClaimsIdentity(
            [new Claim("name", name), new Claim(ClaimTypes.NameIdentifier, name), .. roles.Select(role => new Claim("role", role))],
            "TestAuthType", "name", "role"));

    /// <summary>
    /// Proves the RoleProtectedTools sample from identity.md compiles and correctly allows
    /// admin access to admin-only tools.
    /// </summary>
    [Fact]
    public async Task RoleProtectedTools_AdminTool_AllowsAdminUser()
    {
        Builder.Services.AddMcpServer()
            .WithHttpTransport()
            .AddAuthorizationFilters()
            .WithTools<RoleProtectedTools>();

        Builder.Services.AddAuthorization();

        await using var app = Builder.Build();

        app.Use(next => async context =>
        {
            context.User = CreateUser("AdminUser", "Admin");
            await next(context);
        });

        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync();

        var result = await client.CallToolAsync(
            "admin_operation",
            new Dictionary<string, object?> { ["action"] = "reset" },
            cancellationToken: TestContext.Current.CancellationToken);

        var content = Assert.Single(result.Content.OfType<TextContentBlock>());
        Assert.Equal("Admin action: reset", content.Text);
    }

    /// <summary>
    /// Proves the RoleProtectedTools sample correctly rejects non-admin users from admin tools.
    /// </summary>
    [Fact]
    public async Task RoleProtectedTools_AdminTool_RejectsNonAdminUser()
    {
        Builder.Services.AddMcpServer()
            .WithHttpTransport()
            .AddAuthorizationFilters()
            .WithTools<RoleProtectedTools>();

        Builder.Services.AddAuthorization();

        await using var app = Builder.Build();

        app.Use(next => async context =>
        {
            context.User = CreateUser("RegularUser", "User");
            await next(context);
        });

        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync();

        var exception = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await client.CallToolAsync(
                "admin_operation",
                new Dictionary<string, object?> { ["action"] = "reset" },
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(McpErrorCode.InvalidRequest, exception.ErrorCode);
    }

    /// <summary>
    /// Proves the RoleProtectedTools sample allows anonymous access to [AllowAnonymous] tools.
    /// </summary>
    [Fact]
    public async Task RoleProtectedTools_PublicInfo_AllowsAnonymousAccess()
    {
        Builder.Services.AddMcpServer()
            .WithHttpTransport()
            .AddAuthorizationFilters()
            .WithTools<RoleProtectedTools>();

        Builder.Services.AddAuthorization();

        await using var app = Builder.Build();
        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync();

        var result = await client.CallToolAsync(
            "public_info",
            cancellationToken: TestContext.Current.CancellationToken);

        var content = Assert.Single(result.Content.OfType<TextContentBlock>());
        Assert.Equal("This is public information.", content.Text);
    }

    /// <summary>
    /// Proves the RoleProtectedTools sample filters tool listing based on authorization —
    /// anonymous users only see [AllowAnonymous] tools.
    /// </summary>
    [Fact]
    public async Task RoleProtectedTools_ListTools_FiltersBasedOnAuthorization()
    {
        Builder.Services.AddMcpServer()
            .WithHttpTransport()
            .AddAuthorizationFilters()
            .WithTools<RoleProtectedTools>();

        Builder.Services.AddAuthorization();

        await using var app = Builder.Build();
        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync();
        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Anonymous user should only see the [AllowAnonymous] tool
        Assert.Single(tools);
        Assert.Equal("public_info", tools[0].Name);
    }

    /// <summary>
    /// Proves the RoleProtectedTools sample shows all tools to admin users.
    /// </summary>
    [Fact]
    public async Task RoleProtectedTools_ListTools_AdminSeesAllTools()
    {
        Builder.Services.AddMcpServer()
            .WithHttpTransport()
            .AddAuthorizationFilters()
            .WithTools<RoleProtectedTools>();

        Builder.Services.AddAuthorization();

        await using var app = Builder.Build();
        app.Use(next => async context =>
        {
            context.User = CreateUser("AdminUser", "Admin");
            await next(context);
        });
        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync();
        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Admin should see all three tools
        Assert.Equal(3, tools.Count);
        var toolNames = tools.Select(t => t.Name).OrderBy(n => n).ToList();
        Assert.Equal(["admin_operation", "get_data", "public_info"], toolNames);
    }

    /// <summary>
    /// Proves the HttpContextTools sample from identity.md compiles and works with IHttpContextAccessor.
    /// </summary>
    [Fact]
    public async Task HttpContextTools_GetFilteredData_ReturnsUserName()
    {
        Builder.Services.AddHttpContextAccessor();
        Builder.Services.AddMcpServer()
            .WithHttpTransport()
            .WithTools<HttpContextTools>();

        await using var app = Builder.Build();

        app.Use(next => async context =>
        {
            context.User = CreateUser("HttpUser");
            await next(context);
        });

        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync();

        var result = await client.CallToolAsync(
            "get_filtered_data",
            new Dictionary<string, object?> { ["query"] = "recent items" },
            cancellationToken: TestContext.Current.CancellationToken);

        var content = Assert.Single(result.Content.OfType<TextContentBlock>());
        Assert.Equal("HttpUser: results for 'recent items'", content.Text);
    }

    // --- Doc sample types (exact copies from identity.md) ---

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
}
