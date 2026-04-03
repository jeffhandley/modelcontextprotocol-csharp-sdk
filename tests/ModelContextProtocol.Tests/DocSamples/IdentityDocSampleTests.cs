// Tests that verify the code samples from docs/concepts/identity/identity.md compile and function correctly.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ModelContextProtocol.Tests.Utils;
using System.ComponentModel;
using System.Security.Claims;
using System.Text.Json;

namespace ModelContextProtocol.Tests.DocSamples;

/// <summary>
/// Validates that the code samples in the identity documentation compile and produce correct results.
/// Uses the in-process pipe-based client/server test pattern.
/// </summary>
public class IdentityDocSampleTests(ITestOutputHelper testOutputHelper) : ClientServerTestBase(testOutputHelper, startServer: false)
{
    /// <summary>
    /// Proves the "Direct ClaimsPrincipal Parameter Injection" tool sample from identity.md
    /// compiles and returns the authenticated user's name when identity is set via a message filter.
    /// </summary>
    [Fact]
    public async Task UserAwareTools_Greet_ReturnsUserNameFromClaimsPrincipal()
    {
        McpServerBuilder
            .WithMessageFilters(filters => filters.AddIncomingFilter(next => async (context, cancellationToken) =>
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Name, "Alice")],
                    "TestAuth", ClaimTypes.Name, ClaimTypes.Role));
                await next(context, cancellationToken);
            }))
            .WithTools<UserAwareTools>();

        StartServer();
        await using var client = await CreateMcpClientForServer();

        var result = await client.CallToolAsync(
            "greet",
            new Dictionary<string, object?> { ["message"] = "Hello!" },
            cancellationToken: TestContext.Current.CancellationToken);

        var content = Assert.Single(result.Content.OfType<TextContentBlock>());
        Assert.Equal("Alice: Hello!", content.Text);
    }

    /// <summary>
    /// Proves the "ClaimsPrincipal parameter injection for prompts" sample from identity.md
    /// compiles and returns the user's name in the prompt message.
    /// </summary>
    [Fact]
    public async Task UserAwarePrompts_PersonalizedPrompt_ReturnsUserNameFromClaimsPrincipal()
    {
        McpServerBuilder
            .WithMessageFilters(filters => filters.AddIncomingFilter(next => async (context, cancellationToken) =>
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Name, "Bob")],
                    "TestAuth", ClaimTypes.Name, ClaimTypes.Role));
                await next(context, cancellationToken);
            }))
            .WithPrompts<UserAwarePrompts>();

        StartServer();
        await using var client = await CreateMcpClientForServer();

        var result = await client.GetPromptAsync(
            "personalized_prompt",
            new Dictionary<string, object?> { ["topic"] = "quantum computing" },
            cancellationToken: TestContext.Current.CancellationToken);

        var message = Assert.Single(result.Messages);
        Assert.Equal(Role.User, message.Role);
        var content = Assert.IsType<TextContentBlock>(message.Content);
        Assert.Equal("As Bob, explain quantum computing.", content.Text);
    }

    /// <summary>
    /// Proves the "Accessing Identity in Filters" request filter sample from identity.md.
    /// Verifies that context.User is accessible in a call-tool request filter.
    /// </summary>
    [Fact]
    public async Task RequestFilter_CanAccessUserIdentity()
    {
        string? capturedUserName = null;

        McpServerBuilder
            .WithMessageFilters(filters => filters.AddIncomingFilter(next => async (context, cancellationToken) =>
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Name, "FilterUser")],
                    "TestAuth", ClaimTypes.Name, ClaimTypes.Role));
                await next(context, cancellationToken);
            }))
            .WithRequestFilters(requestFilters =>
            {
                requestFilters.AddCallToolFilter(next => async (context, cancellationToken) =>
                {
                    // This mirrors the doc sample: access user identity in a filter
                    capturedUserName = context.User?.Identity?.Name;
                    var logger = context.Services?.GetService<ILogger<IdentityDocSampleTests>>();
                    logger?.LogInformation("Tool called by: {User}", capturedUserName ?? "anonymous");

                    return await next(context, cancellationToken);
                });
            })
            .WithTools<UserAwareTools>();

        StartServer();
        await using var client = await CreateMcpClientForServer();

        await client.CallToolAsync(
            "greet",
            new Dictionary<string, object?> { ["message"] = "test" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("FilterUser", capturedUserName);
    }

    /// <summary>
    /// Proves the "Setting Identity for Stdio Transport" message filter sample from identity.md.
    /// Uses WithMessageFilters to set identity from simulated environment context, then verifies
    /// the tool receives the ClaimsPrincipal.
    /// </summary>
    [Fact]
    public async Task StdioIdentityPattern_SetsUserViaMessageFilter()
    {
        McpServerBuilder
            .WithMessageFilters(messageFilters =>
            {
                messageFilters.AddIncomingFilter(next => async (context, cancellationToken) =>
                {
                    // This mirrors the doc sample: set user based on process-level context
                    var role = "Admin"; // In real code: Environment.GetEnvironmentVariable("MCP_USER_ROLE") ?? "default"
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.Name, "stdio-user"), new Claim(ClaimTypes.Role, role)],
                        "StdioAuth", ClaimTypes.Name, ClaimTypes.Role));

                    await next(context, cancellationToken);
                });
            })
            .WithTools<UserAwareTools>();

        StartServer();
        await using var client = await CreateMcpClientForServer();

        var result = await client.CallToolAsync(
            "greet",
            new Dictionary<string, object?> { ["message"] = "via stdio" },
            cancellationToken: TestContext.Current.CancellationToken);

        var content = Assert.Single(result.Content.OfType<TextContentBlock>());
        Assert.Equal("stdio-user: via stdio", content.Text);
    }

    /// <summary>
    /// Proves the ClaimsPrincipal is excluded from the generated JSON tool schema —
    /// clients should only see the 'message' parameter, not the 'user' parameter.
    /// </summary>
    [Fact]
    public async Task UserAwareTools_ClaimsPrincipal_ExcludedFromSchema()
    {
        McpServerBuilder.WithTools<UserAwareTools>();

        StartServer();
        await using var client = await CreateMcpClientForServer();

        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        var greetTool = Assert.Single(tools);
        Assert.Equal("greet", greetTool.Name);

        // The schema should have 'message' but NOT 'user'
        var properties = greetTool.JsonSchema.GetProperty("properties");
        Assert.Equal(JsonValueKind.Object, properties.GetProperty("message").ValueKind);
        Assert.False(properties.TryGetProperty("user", out _));
    }

    // --- Doc sample types (exact copies from identity.md) ---

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
}
