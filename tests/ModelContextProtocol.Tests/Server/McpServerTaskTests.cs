using ModelContextProtocol.Client;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

#pragma warning disable MCPEXP001

namespace ModelContextProtocol.Tests.Server;

/// <summary>
/// Tests for the MCP tasks extension (SEP-2663) end-to-end using the store-backed
/// <c>WithTasks</c> public API and the client-side task extension methods.
/// </summary>
public class McpServerTaskTests : ClientServerTestBase
{
    private readonly InMemoryMcpTaskStore _taskStore = new() { DefaultPollIntervalMs = 50 };
    private JsonObject? _capturedMeta;

    public McpServerTaskTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
#if !NET
        Assert.SkipWhen(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "https://github.com/modelcontextprotocol/csharp-sdk/issues/587");
#endif
    }

    protected override void ConfigureServices(ServiceCollection services, IMcpServerBuilder mcpServerBuilder)
    {
        mcpServerBuilder.Services.Configure<McpServerOptions>(options =>
        {
            options.Capabilities ??= new ServerCapabilities();
            options.WithTasks(_taskStore);
        });

        mcpServerBuilder.WithTools([
            // Captures the _meta the server received so the SEP-2575 opt-in envelope can be asserted.
            McpServerTool.Create(
                (RequestContext<CallToolRequestParams> context) =>
                {
                    _capturedMeta = context.Params?.Meta;
                    return "immediate result";
                },
                new() { Name = "immediate-tool" }),

            McpServerTool.Create(
                async (CancellationToken cancellationToken) =>
                {
                    await Task.Delay(100, cancellationToken);
                    return "async result";
                },
                new() { Name = "slow-tool" }),

            McpServerTool.Create(
                string (CancellationToken cancellationToken) => throw new McpException("something went wrong"),
                new() { Name = "failing-tool" }),

            McpServerTool.Create(
                async (CancellationToken cancellationToken) =>
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                    return "never";
                },
                new() { Name = "blocking-tool" }),
        ]);
    }

    [Fact]
    public async Task CallToolAsTaskAsync_ImmediateTool_PollsToResult()
    {
        await using var client = await CreateMcpClientForServer();

        var result = await client.CallToolAsTaskAsync(
            new CallToolRequestParams { Name = "immediate-tool" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Single(result.Content);
        Assert.Equal("immediate result", Assert.IsType<TextContentBlock>(result.Content[0]).Text);
    }

    [Fact]
    public async Task CallToolRawAsync_OptedIn_ReturnsTaskCreated()
    {
        await using var client = await CreateMcpClientForServer();

        var augmented = await client.CallToolRawAsync(
            new CallToolRequestParams { Name = "immediate-tool" },
            TestContext.Current.CancellationToken);

        Assert.True(augmented.IsTask);
        Assert.NotNull(augmented.TaskCreated);
        Assert.Null(augmented.Result);
        Assert.Equal(McpTaskStatus.Working, augmented.TaskCreated.Status);
        Assert.Equal("task", augmented.TaskCreated.ResultType);
    }

    [Fact]
    public async Task CallToolAsTaskAsync_SlowTool_PollsUntilCompleted()
    {
        await using var client = await CreateMcpClientForServer();
        var ct = TestContext.Current.CancellationToken;

        var result = await client.CallToolAsTaskAsync(
            new CallToolRequestParams { Name = "slow-tool" },
            cancellationToken: ct);

        Assert.NotNull(result);
        Assert.Single(result.Content);
        Assert.Equal("async result", Assert.IsType<TextContentBlock>(result.Content[0]).Text);
    }

    [Fact]
    public async Task CallToolAsTaskAsync_FailingTool_ReturnsErrorResult()
    {
        await using var client = await CreateMcpClientForServer();
        var ct = TestContext.Current.CancellationToken;

        var result = await client.CallToolAsTaskAsync(
            new CallToolRequestParams { Name = "failing-tool" },
            cancellationToken: ct);

        Assert.NotNull(result);
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task GetTaskAsync_ReturnsCurrentState()
    {
        await using var client = await CreateMcpClientForServer();
        var ct = TestContext.Current.CancellationToken;

        var augmented = await client.CallToolRawAsync(
            new CallToolRequestParams { Name = "blocking-tool" }, ct);

        var taskId = augmented.TaskCreated!.TaskId;

        var taskResult = await client.GetTaskAsync(taskId, ct);
        Assert.IsType<WorkingTaskResult>(taskResult);
        Assert.Equal(taskId, taskResult.TaskId);
        Assert.Equal(McpTaskStatus.Working, taskResult.Status);

        // Release the blocking task so it doesn't linger in the background.
        await client.CancelTaskAsync(taskId, ct);
    }

    [Fact]
    public async Task CancelTaskAsync_CancelsTask()
    {
        await using var client = await CreateMcpClientForServer();
        var ct = TestContext.Current.CancellationToken;

        var augmented = await client.CallToolRawAsync(
            new CallToolRequestParams { Name = "blocking-tool" }, ct);

        var taskId = augmented.TaskCreated!.TaskId;

        var cancelResult = await client.CancelTaskAsync(taskId, ct);
        Assert.NotNull(cancelResult);

        GetTaskResult? taskResult = null;
        for (int i = 0; i < 40; i++)
        {
            taskResult = await client.GetTaskAsync(taskId, ct);
            if (taskResult is CancelledTaskResult)
            {
                break;
            }

            await Task.Delay(50, ct);
        }

        Assert.IsType<CancelledTaskResult>(taskResult);
    }

    [Fact]
    public async Task ConfigureTasks_AdvertisesExtensionInCapabilities()
    {
        await using var client = await CreateMcpClientForServer();

        // The server advertises the tasks extension during initialize.
        // The client should see it in server capabilities after the handshake.
        #pragma warning disable MCP_EXTENSIONS
        var extensions = client.ServerCapabilities.Extensions;
        #pragma warning restore MCP_EXTENSIONS
        Assert.NotNull(extensions);
        Assert.True(extensions.ContainsKey(McpExtensions.Tasks));
    }

    [Fact]
    public async Task CreateTaskResult_HasResultTypeTask()
    {
        await using var client = await CreateMcpClientForServer();

        var augmented = await client.CallToolRawAsync(
            new CallToolRequestParams { Name = "immediate-tool" },
            TestContext.Current.CancellationToken);

        Assert.True(augmented.IsTask);
        Assert.Equal("task", augmented.TaskCreated!.ResultType);
    }

    [Fact]
    public async Task GetTaskAsync_ImmediatelyAfterCreate_Resolves()
    {
        // Strong consistency: tasks/get immediately after CreateTaskResult must resolve.
        await using var client = await CreateMcpClientForServer();
        var ct = TestContext.Current.CancellationToken;

        var augmented = await client.CallToolRawAsync(
            new CallToolRequestParams { Name = "blocking-tool" }, ct);

        var taskId = augmented.TaskCreated!.TaskId;

        // No delay — immediate get
        var taskResult = await client.GetTaskAsync(taskId, ct);
        Assert.NotNull(taskResult);
        Assert.Equal(taskId, taskResult.TaskId);

        await client.CancelTaskAsync(taskId, ct);
    }

    [Fact]
    public async Task GetTaskAsync_UnknownTaskId_ThrowsWithInvalidParams()
    {
        await using var client = await CreateMcpClientForServer();

        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            await client.GetTaskAsync("nonexistent-task-id-12345", TestContext.Current.CancellationToken));

        // The server should reject with an error referencing the unknown task
        Assert.Contains("Unknown task", ex.Message);
    }

    [Fact]
    public async Task CancelTask_AlreadyTerminal_AcknowledgesIdempotently()
    {
        await using var client = await CreateMcpClientForServer();
        var ct = TestContext.Current.CancellationToken;

        var augmented = await client.CallToolRawAsync(
            new CallToolRequestParams { Name = "blocking-tool" }, ct);
        var taskId = augmented.TaskCreated!.TaskId;

        // Cancel once
        await client.CancelTaskAsync(taskId, ct);

        // Cancel again on terminal task — should not throw, returns ack
        var ack = await client.CancelTaskAsync(taskId, ct);
        Assert.NotNull(ack);
    }

    [Fact]
    public async Task CallToolRawAsync_OptIn_UsesSep2575CapabilitiesEnvelope()
    {
        // SEP-2663 §51: the per-request opt-in is the SEP-2575 capabilities envelope:
        //   _meta/io.modelcontextprotocol/clientCapabilities/extensions/io.modelcontextprotocol/tasks = {}
        // This test pins the literal wire path so future refactors can't regress.
        await using var client = await CreateMcpClientForServer();

        // CallToolAsTaskAsync runs the tool to completion, ensuring the server-side tool has
        // observed the request _meta before we assert on the captured value.
        await client.CallToolAsTaskAsync(
            new CallToolRequestParams { Name = "immediate-tool" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(_capturedMeta);

        var caps = Assert.IsType<JsonObject>(_capturedMeta!["io.modelcontextprotocol/clientCapabilities"]);
        var extensions = Assert.IsType<JsonObject>(caps["extensions"]);
        Assert.True(extensions.ContainsKey("io.modelcontextprotocol/tasks"),
            "Expected _meta to contain io.modelcontextprotocol/clientCapabilities/extensions/io.modelcontextprotocol/tasks (SEP-2575 envelope).");

        // The opt-in value is an empty object per SEP-2575.
        Assert.IsType<JsonObject>(extensions["io.modelcontextprotocol/tasks"]);
    }

    [Fact]
    public async Task CallToolRawAsync_OptIn_PreservesExistingMetaSiblings()
    {
        // User-supplied _meta entries at the root must not be clobbered, and the SEP-2575
        // envelope must be added alongside them, not in place of them.
        await using var client = await CreateMcpClientForServer();

        var userMeta = new JsonObject
        {
            ["customKey"] = "customValue",
            ["io.modelcontextprotocol/clientCapabilities"] = new JsonObject
            {
                ["extensions"] = new JsonObject
                {
                    ["some.other/extension"] = new JsonObject(),
                },
            },
        };

        await client.CallToolAsTaskAsync(
            new CallToolRequestParams
            {
                Name = "immediate-tool",
                Meta = userMeta,
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(_capturedMeta);

        // User's sibling root entry is preserved.
        Assert.Equal("customValue", (string?)_capturedMeta!["customKey"]);

        // User's pre-existing nested extension is preserved next to the tasks opt-in.
        var caps = Assert.IsType<JsonObject>(_capturedMeta["io.modelcontextprotocol/clientCapabilities"]);
        var extensions = Assert.IsType<JsonObject>(caps["extensions"]);
        Assert.True(extensions.ContainsKey("some.other/extension"));
        Assert.True(extensions.ContainsKey("io.modelcontextprotocol/tasks"));
    }
}
