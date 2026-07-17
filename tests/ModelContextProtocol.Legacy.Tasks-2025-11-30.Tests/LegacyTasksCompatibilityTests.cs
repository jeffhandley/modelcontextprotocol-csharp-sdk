using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Legacy.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ModelContextProtocol.Tests;
using System.ComponentModel;
using System.Text.Json;

namespace ModelContextProtocol.Legacy.Tasks.Tests;

public class LegacyTasksCompatibilityTests(ITestOutputHelper testOutputHelper) : ClientServerTestBase(testOutputHelper)
{
    protected override void ConfigureServices(ServiceCollection services, IMcpServerBuilder mcpServerBuilder)
    {
        mcpServerBuilder
            .WithLegacyTasks(new InMemoryLegacyMcpTaskStore())
            .WithTasks(new InMemoryMcpTaskStore())
            .WithTools<LegacyTaskTools>();
    }

    [Fact]
    public async Task LegacyProtocol_ExposesTaskLifecycleAlongsideModernSdk()
    {
        var clientOptions = new McpClientOptions().EnableLegacyTasks();
        await using var client = await CreateMcpClientForServer(clientOptions);
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal(LegacyTasksProtocol.ProtocolVersion, client.NegotiatedProtocolVersion);
        Assert.True(client.ServerCapabilities.AdditionalProperties?.ContainsKey(LegacyTasksProtocol.CapabilityName));

        var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
        var tool = Assert.Single(tools, tool => tool.Name == "legacy-echo");
        Assert.Equal(
            "optional",
            tool.ProtocolTool.AdditionalProperties!["execution"].GetProperty("taskSupport").GetString());

        var started = await client.CallToolAsLegacyTaskAsync(
            new CallToolRequestParams { Name = "legacy-echo" },
            cancellationToken: cancellationToken);

        Assert.True(started.IsTask);
        Assert.NotNull(started.Task);

        var listed = await client.ListLegacyTasksAsync(cancellationToken: cancellationToken);
        Assert.Contains(listed.Tasks, task => task.TaskId == started.Task.TaskId);

        var payload = await client.GetLegacyTaskPayloadAsync(started.Task.TaskId, cancellationToken);
        var result = JsonSerializer.Deserialize(payload, McpJsonUtilities.DefaultOptions.GetTypeInfo<CallToolResult>());
        Assert.NotNull(result);
        Assert.Equal("legacy result", Assert.IsType<TextContentBlock>(result.Content[0]).Text);

        var completed = await client.GetLegacyTaskAsync(started.Task.TaskId, cancellationToken);
        Assert.Equal(McpLegacyTaskStatus.Completed, completed.Status);
    }

    [Fact]
    public async Task LegacyProtocol_CancelTaskRoutesToLegacyTaskStore()
    {
        var clientOptions = new McpClientOptions().EnableLegacyTasks();
        await using var client = await CreateMcpClientForServer(clientOptions);
        var cancellationToken = TestContext.Current.CancellationToken;

        var started = await client.CallToolAsLegacyTaskAsync(
            new CallToolRequestParams { Name = "legacy-wait" },
            cancellationToken: cancellationToken);

        var cancelled = await client.CancelLegacyTaskAsync(started.Task!.TaskId, cancellationToken);
        Assert.Equal(McpLegacyTaskStatus.Cancelled, cancelled.Status);

        var task = await client.GetLegacyTaskAsync(started.Task.TaskId, cancellationToken);
        Assert.Equal(McpLegacyTaskStatus.Cancelled, task.Status);
    }

    [Fact]
    public void EnableLegacyTasks_RejectsAnotherPinnedProtocolVersion()
    {
        var options = new McpClientOptions { ProtocolVersion = "2025-11-25" };

        var exception = Assert.Throws<InvalidOperationException>(() => options.EnableLegacyTasks());

        Assert.Contains(LegacyTasksProtocol.ProtocolVersion, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ModernProtocol_UsesModernTasksWhenLegacyTasksAreAlsoRegistered()
    {
        await using var client = await CreateMcpClientForServer();
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.True(client.ServerCapabilities.Extensions?.ContainsKey(TasksProtocol.ExtensionId) is true);

        var started = await client.CallToolAsTaskAsync(
            new CallToolRequestParams { Name = "legacy-echo" },
            cancellationToken);

        Assert.True(started.IsTask);
        Assert.NotNull(started.TaskCreated);

        var task = await client.GetTaskAsync(started.TaskCreated.TaskId, cancellationToken);
        Assert.NotNull(task);
    }

    [Fact]
    public async Task TaskMigration_UsesModernTasksWithoutInfluencingProtocolNegotiation()
    {
        var clientOptions = new McpClientOptions().EnableTasksMigration();
        Assert.Null(clientOptions.ProtocolVersion);
        Assert.True(clientOptions.Capabilities!.AdditionalProperties!.ContainsKey(LegacyTasksProtocol.CapabilityName));

        await using var client = await CreateMcpClientForServer(clientOptions);
        var migrationClient = client.CreateTaskMigrationClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal(McpTaskMigrationMode.Modern, migrationClient.Mode);
        var taskTool = Assert.Single(
            await client.ListToolsAsync(cancellationToken: cancellationToken),
            tool => tool.Name == "legacy-echo");
        Assert.True(migrationClient.SupportsTaskExecution(taskTool));

        var started = await migrationClient.CallToolAsTaskAsync(
            new CallToolRequestParams { Name = "legacy-echo" },
            cancellationToken);

        Assert.True(started.IsTask);
        Assert.NotNull(started.ModernTask);
        Assert.Null(started.LegacyTask);

        var result = await migrationClient.CallToolWithPollingAsync(
            new CallToolRequestParams { Name = "legacy-echo" },
            cancellationToken: cancellationToken);
        Assert.Equal("legacy result", Assert.IsType<TextContentBlock>(result.Content[0]).Text);
    }

    [McpServerToolType]
    private sealed class LegacyTaskTools
    {
        [McpServerTool(Name = "legacy-echo"), Description("Returns a value through a legacy task.")]
        public static string LegacyEcho() => "legacy result";

        [McpServerTool(Name = "legacy-wait"), Description("Waits until a legacy task is cancelled.")]
        public static async Task<string> LegacyWait(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return "unreachable";
        }
    }
}
