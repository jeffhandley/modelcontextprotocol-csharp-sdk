using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Legacy.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ModelContextProtocol.Tests;
using ModelContextProtocol.Tests.Utils;
using System.ComponentModel;
using System.Text.Json;

namespace ModelContextProtocol.Legacy.Tasks.Tests;

public class LegacyTasksMigrationFallbackTests(ITestOutputHelper testOutputHelper) : ClientServerTestBase(testOutputHelper)
{
    private readonly InMemoryLegacyMcpTaskStore _taskStore = new();

    protected override void ConfigureServices(ServiceCollection services, IMcpServerBuilder mcpServerBuilder)
    {
        services.Configure<McpServerOptions>(options =>
        {
            options.ProtocolVersion = LegacyTasksProtocol.ProtocolVersion;
            options.ServerInfo = new Implementation { Name = "legacy-tasks-test-server", Version = "1.0" };
        });

        mcpServerBuilder
            .WithLegacyTasks(_taskStore)
            .WithTools<LegacyTaskTools>();
    }

    [Fact]
    public async Task TaskMigration_FallsBackToLegacyTasksAndLogsTheServer()
    {
        var clientOptions = new McpClientOptions().EnableTasksMigration();
        Assert.Null(clientOptions.ProtocolVersion);

        await using var client = await CreateMcpClientForServer(clientOptions);
        var migrationClient = client.CreateTaskMigrationClient(MockLoggerProvider.CreateLogger("TasksMigration"));
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal(LegacyTasksProtocol.ProtocolVersion, client.NegotiatedProtocolVersion);
        Assert.Equal(McpTaskMigrationMode.Legacy, migrationClient.Mode);

        var started = await migrationClient.CallToolAsTaskAsync(
            new CallToolRequestParams { Name = "legacy-echo" },
            cancellationToken);

        Assert.True(started.IsTask);
        Assert.NotNull(started.LegacyTask);
        Assert.Null(started.ModernTask);

        var result = await migrationClient.CallToolWithPollingAsync(
            new CallToolRequestParams { Name = "legacy-echo" },
            cancellationToken: cancellationToken);
        Assert.Equal("legacy result", Assert.IsType<TextContentBlock>(result.Content[0]).Text);

        Assert.Contains(
            MockLoggerProvider.LogMessages,
            message => message.LogLevel == Microsoft.Extensions.Logging.LogLevel.Information &&
                       message.Message.Contains("legacy-tasks-test-server", StringComparison.Ordinal) &&
                       message.Message.Contains(LegacyTasksProtocol.ProtocolVersion, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TaskMigration_LegacyToolMetadataSelectsTheTaskPipeline()
    {
        await using var client = await CreateMcpClientForServer(new McpClientOptions().EnableTasksMigration());
        var migrationClient = client.CreateTaskMigrationClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        var taskTool = Assert.Single(await client.ListToolsAsync(cancellationToken: cancellationToken));

        Assert.Equal(
            "optional",
            taskTool.ProtocolTool.AdditionalProperties!["execution"].GetProperty("taskSupport").GetString());
        Assert.True(migrationClient.SupportsTaskExecution(taskTool));

        var result = await migrationClient.ExecuteToolAsync(
            taskTool,
            new CallToolRequestParams { Name = taskTool.ProtocolTool.Name },
            cancellationToken: cancellationToken);
        Assert.Equal("legacy result", Assert.IsType<TextContentBlock>(result.Content[0]).Text);
        Assert.Single((await client.ListLegacyTasksAsync(cancellationToken: cancellationToken)).Tasks);

        var directTool = new McpClientTool(client, new Tool { Name = taskTool.ProtocolTool.Name });
        Assert.False(migrationClient.SupportsTaskExecution(directTool));

        result = await migrationClient.ExecuteToolAsync(
            directTool,
            new CallToolRequestParams { Name = directTool.ProtocolTool.Name },
            cancellationToken: cancellationToken);
        Assert.Equal("legacy result", Assert.IsType<TextContentBlock>(result.Content[0]).Text);
        Assert.Single((await client.ListLegacyTasksAsync(cancellationToken: cancellationToken)).Tasks);
    }

    [Fact]
    public async Task LegacyToolDescriptor_ExecutionMetadataIsPreservedWithoutForcingTaskExecution()
    {
        await using var client = await CreateMcpClientForServer();
        var cancellationToken = TestContext.Current.CancellationToken;
        var tool = Assert.Single(await client.ListToolsAsync(cancellationToken: cancellationToken));

        JsonElement execution = tool.ProtocolTool.AdditionalProperties!["execution"];
        Assert.Equal("""{"taskSupport":"optional"}""", execution.GetRawText());
        Assert.Equal("optional", execution.GetProperty("taskSupport").GetString());

        var result = await tool.CallAsync(cancellationToken: cancellationToken);

        Assert.Equal("legacy result", Assert.IsType<TextContentBlock>(result.Content[0]).Text);
        Assert.Empty((await _taskStore.ListTasksAsync(cancellationToken: cancellationToken)).Tasks);
    }

    [McpServerToolType]
    private sealed class LegacyTaskTools
    {
        [McpServerTool(Name = "legacy-echo"), Description("Returns a value through a legacy task.")]
        public static string LegacyEcho() => "legacy result";
    }
}
