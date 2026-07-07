using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Runtime.InteropServices;
using System.Text.Json;

#pragma warning disable MCPEXP001

namespace ModelContextProtocol.Tests.Server;

/// <summary>
/// Exercises the client-side guard that prevents an unbounded poll loop when a server keeps a
/// task in <see cref="McpTaskStatus.InputRequired"/> without publishing any new input requests
/// after every previously requested input has been resolved.
/// </summary>
public class TaskPollStuckDetectorTests : ClientServerTestBase
{
    private readonly StuckInputRequiredStore _store = new();

    public TaskPollStuckDetectorTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
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

            // The store always reports the task as InputRequired with no outstanding input
            // requests, which is the misbehaving-server condition the stuck-detector exists
            // to break out of.
            options.WithTasks(_store);
        });

        mcpServerBuilder.WithTools([McpServerTool.Create(
            (CancellationToken ct) => "ok",
            new() { Name = "any-tool" })]);
    }

    [Fact]
    public async Task CallToolAsTaskAsync_TaskStuckInInputRequired_WithoutNewRequests_ThrowsAfterThreshold()
    {
        await using var client = await CreateMcpClientForServer();
        var ct = TestContext.Current.CancellationToken;

        var ex = await Assert.ThrowsAsync<McpException>(async () =>
            await client.CallToolAsTaskAsync(new CallToolRequestParams { Name = "any-tool" }, cancellationToken: ct));

        Assert.Contains(McpTaskStatus.InputRequired.ToString(), ex.Message);
        Assert.Contains("consecutive polls", ex.Message);

        Assert.Equal(McpClientTasksExtensions.DefaultMaxConsecutiveStuckPolls, _store.PollCount);
    }

    [Fact]
    public async Task CallToolAsTaskAsync_StuckDetector_HonorsConfiguredThreshold()
    {
        // Verifies the maxConsecutiveStuckPolls argument is plumbed into PollTaskToCompletionAsync:
        // a smaller configured threshold is surfaced verbatim in the McpException message.
        const int CustomThreshold = 3;

        await using var client = await CreateMcpClientForServer();
        var ct = TestContext.Current.CancellationToken;

        var ex = await Assert.ThrowsAsync<McpException>(async () =>
            await client.CallToolAsTaskAsync(
                new CallToolRequestParams { Name = "any-tool" },
                maxConsecutiveStuckPolls: CustomThreshold,
                cancellationToken: ct));

        // The message embeds the configured threshold, which is the strongest signal that the
        // argument value (not the default constant) is what governed the loop.
        Assert.Contains($"{CustomThreshold} consecutive polls", ex.Message);
        Assert.Equal(CustomThreshold, _store.PollCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task CallToolAsTaskAsync_MaxConsecutiveStuckPolls_RejectsNonPositive(int value)
    {
        await using var client = await CreateMcpClientForServer();
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await client.CallToolAsTaskAsync(
                new CallToolRequestParams { Name = "any-tool" },
                maxConsecutiveStuckPolls: value,
                cancellationToken: ct));
    }

    [Fact]
    public void DefaultMaxConsecutiveStuckPolls_Is60()
    {
        Assert.Equal(60, McpClientTasksExtensions.DefaultMaxConsecutiveStuckPolls);
    }

    /// <summary>
    /// A task store that always reports the task as <see cref="McpTaskStatus.InputRequired"/> with no
    /// outstanding input requests, simulating a misbehaving server that never makes progress.
    /// </summary>
    private sealed class StuckInputRequiredStore : IMcpTaskStore
    {
        private int _pollCount;

        public int PollCount => Volatile.Read(ref _pollCount);

        public event Action<InputResponseReceivedEventArgs>? InputResponseReceived
        {
            add { }
            remove { }
        }

        public Task<McpTaskInfo> CreateTaskAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new McpTaskInfo(
                Guid.NewGuid().ToString("N"),
                McpTaskStatus.Working,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                PollIntervalMs: 5));

        public Task<McpTaskInfo?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _pollCount);

            return Task.FromResult<McpTaskInfo?>(new McpTaskInfo(
                taskId,
                McpTaskStatus.InputRequired,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                PollIntervalMs: 5,
                InputRequests: new Dictionary<string, InputRequest>()));
        }

        public Task SetCompletedAsync(string taskId, JsonElement result, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetFailedAsync(string taskId, JsonElement error, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> SetCancelledAsync(string taskId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task ResolveInputRequestsAsync(string taskId, IDictionary<string, InputResponse> inputResponses, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetInputRequestsAsync(string taskId, IDictionary<string, InputRequest> inputRequests, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
