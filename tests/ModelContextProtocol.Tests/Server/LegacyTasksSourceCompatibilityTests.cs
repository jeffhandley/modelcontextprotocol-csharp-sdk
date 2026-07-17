#pragma warning disable MCP9007

using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Legacy.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace ModelContextProtocol.Tests.Server;

public class LegacyTasksSourceCompatibilityTests(ITestOutputHelper testOutputHelper) : ClientServerTestBase(testOutputHelper)
{
    protected override void ConfigureServices(ServiceCollection services, IMcpServerBuilder mcpServerBuilder)
    {
        services.Configure<McpServerOptions>(options => options.ProtocolVersion = LegacyTasksProtocol.ProtocolVersion);
        mcpServerBuilder
            .WithLegacyTasks(new InMemoryLegacyMcpTaskStore())
            .WithTools<LegacyTaskTools>();
    }

    [Fact]
    public async Task LegacyTaskClientApi_SourceCompatibleMethodsUseLegacyTaskLifecycle()
    {
        await using var client = await CreateMcpClientForServer(new McpClientOptions().EnableLegacyTasks());
        var cancellationToken = TestContext.Current.CancellationToken;

        McpTask created = await client.CallToolAsTaskAsync(
            "legacy-echo",
            taskMetadata: new McpTaskMetadata { TimeToLive = TimeSpan.FromMinutes(5) },
            cancellationToken: cancellationToken);
        Assert.Equal(McpTaskStatus.Working, created.Status);

        McpTask fetched = await client.GetTaskAsync(created.TaskId, cancellationToken: cancellationToken);
        Assert.Equal(created.TaskId, fetched.TaskId);

        IList<McpTask> allTasks = await client.ListTasksAsync(cancellationToken: cancellationToken);
        Assert.Contains(allTasks, task => task.TaskId == created.TaskId);

        ListTasksResult page = await client.ListTasksAsync(
            new ListTasksRequestParams(),
            cancellationToken);
        Assert.Contains(page.Tasks, task => task.TaskId == created.TaskId);

        McpTask terminal = await client.PollTaskUntilCompleteAsync(created.TaskId, cancellationToken: cancellationToken);
        Assert.Equal(McpTaskStatus.Completed, terminal.Status);

        JsonElement payload = await client.GetTaskResultAsync(created.TaskId, cancellationToken: cancellationToken);
        var result = JsonSerializer.Deserialize(payload, McpJsonUtilities.DefaultOptions.GetTypeInfo<CallToolResult>());
        Assert.Equal("legacy result", Assert.IsType<TextContentBlock>(result!.Content[0]).Text);
    }

    [Fact]
    public async Task LegacyTaskClientApi_CancelTaskUsesOriginalMethodName()
    {
        await using var client = await CreateMcpClientForServer(new McpClientOptions().EnableLegacyTasks());
        var cancellationToken = TestContext.Current.CancellationToken;

        McpTask created = await client.CallToolAsTaskAsync("legacy-wait", cancellationToken: cancellationToken);
        McpTask cancelled = await client.CancelTaskAsync(created.TaskId, cancellationToken: cancellationToken);

        Assert.Equal(McpTaskStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public void LegacyTaskCompatibilityTypes_ReportCustomObsolescenceDiagnostic()
    {
        Assert.Equal("MCP9007", GetObsolescenceDiagnosticId(typeof(McpTask)));
        Assert.Equal("MCP9007", GetObsolescenceDiagnosticId(typeof(LegacyMcpClientTaskExtensions)));
    }

    private static string? GetObsolescenceDiagnosticId(Type type) =>
        type.CustomAttributes
            .Single(attribute => attribute.AttributeType.FullName == "System.ObsoleteAttribute")
            .NamedArguments
            .Single(argument => argument.MemberName == "DiagnosticId")
            .TypedValue
            .Value as string;

    [McpServerToolType]
    private sealed class LegacyTaskTools
    {
        [McpServerTool(Name = "legacy-echo"), Description("Returns a legacy task result.")]
        public static string LegacyEcho() => "legacy result";

        [McpServerTool(Name = "legacy-wait"), Description("Waits until its legacy task is cancelled.")]
        public static async Task<string> LegacyWait(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return "unreachable";
        }
    }
}
