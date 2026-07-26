using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Docs.Snippets.Tasks;

internal static class TaskServer
{
    public static void ConfigureStore(WebApplicationBuilder builder)
    {
        // <snippet_WithTasks>
        builder.Services.AddMcpServer()
            .WithTools<MyTools>()
            .WithTasks(new InMemoryMcpTaskStore());
        // </snippet_WithTasks>
    }

    public static void ConfigureAlternateHandler(McpServerOptions options)
    {
#pragma warning disable MCPEXP002 // ResultOrAlternate is an experimental extensibility seam.
        // <snippet_CallToolWithAlternate>
        options.Handlers.CallToolWithAlternateHandler = async (context, ct) =>
        {
            if (ShouldRunInline(context.Params!))
            {
                return new CallToolResult { Content = [/* … */] };
            }

            var taskId = await StartBackgroundWorkAsync(context.Params!, ct);
            var created = new CreateTaskResult
            {
                TaskId = taskId,
                Status = McpTaskStatus.Working,
                CreatedAt = DateTimeOffset.UtcNow,
                LastUpdatedAt = DateTimeOffset.UtcNow,
                PollIntervalMs = 1000,
            };

            return ResultOrAlternate<CallToolResult>.FromAlternate(created, McpTasksJsonContext.Default.CreateTaskResult);
        };
        // </snippet_CallToolWithAlternate>
#pragma warning restore MCPEXP002
    }

    private static bool ShouldRunInline(CallToolRequestParams request) => true;

    private static Task<string> StartBackgroundWorkAsync(CallToolRequestParams request, CancellationToken cancellationToken)
        => Task.FromResult("task-id");
}

internal static class TaskClient
{
    public static async Task PollTool(McpClient client, IDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
    {
        // <snippet_CallToolWithPolling>
        var result = await client.CallToolWithPollingAsync(
            new CallToolRequestParams { Name = "long-running-tool", Arguments = arguments },
            cancellationToken: cancellationToken);
        // </snippet_CallToolWithPolling>
    }

    public static async Task DriveTask(McpClient client, CallToolRequestParams requestParams, CancellationToken cancellationToken)
    {
        // <snippet_CallToolAsTask>
        var raw = await client.CallToolAsTaskAsync(requestParams, cancellationToken);
        if (raw.IsTask)
        {
            var taskId = raw.TaskCreated!.TaskId;
            while (true)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(raw.TaskCreated!.PollIntervalMs ?? 1000), cancellationToken);
                var state = await client.GetTaskAsync(taskId, cancellationToken);
                // Handle InputRequiredTaskResult by calling UpdateTaskAsync,
                // CompletedTaskResult by deserializing its Result property, etc.
            }
        }
        // </snippet_CallToolAsTask>
    }
}

internal class MyTools;
