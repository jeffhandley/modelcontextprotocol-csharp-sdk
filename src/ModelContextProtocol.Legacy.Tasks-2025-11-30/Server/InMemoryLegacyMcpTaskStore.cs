using System.Collections.Concurrent;
using System.Text.Json;

namespace ModelContextProtocol.Legacy.Tasks;

/// <summary>Provides an in-memory task store for development, tests, and single-process compatibility scenarios.</summary>
public sealed class InMemoryLegacyMcpTaskStore : ILegacyMcpTaskStore
{
    private readonly ConcurrentDictionary<string, Entry> _tasks = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the poll interval assigned to newly created tasks.</summary>
    public TimeSpan DefaultPollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Creates a task in the working state.</summary>
    public Task<McpLegacyTask> CreateTaskAsync(McpLegacyTaskMetadata metadata, CancellationToken cancellationToken = default)
    {
        if (metadata is null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        var now = DateTimeOffset.UtcNow;
        var task = new McpLegacyTask
        {
            TaskId = Guid.NewGuid().ToString("N"),
            Status = McpLegacyTaskStatus.Working,
            CreatedAt = now,
            LastUpdatedAt = now,
            TimeToLive = metadata.TimeToLive,
            PollInterval = DefaultPollInterval,
        };

        _tasks[task.TaskId] = new Entry(task);
        return Task.FromResult(Clone(task));
    }

    /// <summary>Gets a task by identifier.</summary>
    public Task<McpLegacyTask?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        if (taskId is null)
        {
            throw new ArgumentNullException(nameof(taskId));
        }

        if (!_tasks.TryGetValue(taskId, out var entry))
        {
            return Task.FromResult<McpLegacyTask?>(null);
        }

        lock (entry.Gate)
        {
            return Task.FromResult<McpLegacyTask?>(Clone(entry.Task));
        }
    }

    /// <summary>Stores a completed or failed result.</summary>
    public Task<McpLegacyTask> StoreTaskResultAsync(
        string taskId,
        McpLegacyTaskStatus status,
        JsonElement result,
        CancellationToken cancellationToken = default)
    {
        if (status is not (McpLegacyTaskStatus.Completed or McpLegacyTaskStatus.Failed))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "Only completed or failed task results can be stored.");
        }

        var entry = GetRequiredEntry(taskId);
        lock (entry.Gate)
        {
            if (!IsTerminal(entry.Task.Status))
            {
                entry.Task.Status = status;
                entry.Task.LastUpdatedAt = DateTimeOffset.UtcNow;
                entry.Result = result.Clone();
                entry.Completion.TrySetResult(null);
            }

            return Task.FromResult(Clone(entry.Task));
        }
    }

    /// <summary>Waits for and returns a task's terminal result.</summary>
    public async Task<JsonElement> GetTaskResultAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var entry = GetRequiredEntry(taskId);
        await WaitWithCancellationAsync(entry.Completion.Task, cancellationToken).ConfigureAwait(false);

        lock (entry.Gate)
        {
            return entry.Result?.Clone()
                ?? throw new InvalidOperationException($"Task '{taskId}' completed without a result payload.");
        }
    }

    /// <summary>Lists all in-memory tasks.</summary>
    public Task<ListLegacyTasksResult> ListTasksAsync(string? cursor = null, CancellationToken cancellationToken = default)
    {
        if (cursor is not null)
        {
            throw new ArgumentException("InMemoryLegacyMcpTaskStore does not support pagination cursors.", nameof(cursor));
        }

        var tasks = _tasks.Values
            .Select(entry =>
            {
                lock (entry.Gate)
                {
                    return Clone(entry.Task);
                }
            })
            .OrderBy(task => task.CreatedAt)
            .ToList();

        return Task.FromResult(new ListLegacyTasksResult { Tasks = tasks });
    }

    /// <summary>Cancels a non-terminal task.</summary>
    public Task<McpLegacyTask> CancelTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var entry = GetRequiredEntry(taskId);
        lock (entry.Gate)
        {
            if (!IsTerminal(entry.Task.Status))
            {
                entry.Task.Status = McpLegacyTaskStatus.Cancelled;
                entry.Task.LastUpdatedAt = DateTimeOffset.UtcNow;
                entry.Completion.TrySetResult(null);
            }

            return Task.FromResult(Clone(entry.Task));
        }
    }

    private Entry GetRequiredEntry(string taskId)
    {
        if (taskId is null)
        {
            throw new ArgumentNullException(nameof(taskId));
        }

        return _tasks.TryGetValue(taskId, out var entry)
            ? entry
            : throw new InvalidOperationException($"Task '{taskId}' was not found.");
    }

    private static bool IsTerminal(McpLegacyTaskStatus status) =>
        status is McpLegacyTaskStatus.Completed or McpLegacyTaskStatus.Failed or McpLegacyTaskStatus.Cancelled;

    private static McpLegacyTask Clone(McpLegacyTask task) =>
        new()
        {
            TaskId = task.TaskId,
            Status = task.Status,
            StatusMessage = task.StatusMessage,
            CreatedAt = task.CreatedAt,
            LastUpdatedAt = task.LastUpdatedAt,
            TimeToLive = task.TimeToLive,
            PollInterval = task.PollInterval,
        };

    private static async Task WaitWithCancellationAsync(Task task, CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled || task.IsCompleted)
        {
            await task.ConfigureAwait(false);
            return;
        }

        var cancellation = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(
            static state => ((TaskCompletionSource<object?>)state!).TrySetResult(null),
            cancellation);

        if (await Task.WhenAny(task, cancellation.Task).ConfigureAwait(false) != task)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        await task.ConfigureAwait(false);
    }

    private sealed class Entry(McpLegacyTask task)
    {
        public object Gate { get; } = new();
        public McpLegacyTask Task { get; } = task;
        public JsonElement? Result { get; set; }
        public TaskCompletionSource<object?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
