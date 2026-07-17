using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ModelContextProtocol.Legacy.Tasks;

/// <summary>Provides client APIs for the 2025-11-30 Tasks draft.</summary>
public static class LegacyTasksClientExtensions
{
    /// <summary>Configures a client to negotiate and advertise the legacy Tasks draft.</summary>
    public static McpClientOptions EnableLegacyTasks(this McpClientOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.ProtocolVersion is not null &&
            !string.Equals(options.ProtocolVersion, LegacyTasksProtocol.ProtocolVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Legacy Tasks requires protocol version '{LegacyTasksProtocol.ProtocolVersion}', but the client is configured for '{options.ProtocolVersion}'.");
        }

        options.ProtocolVersion = LegacyTasksProtocol.ProtocolVersion;
        AddLegacyTasksCapability(options);
        return options;
    }

    /// <summary>
    /// Advertises support for the legacy Tasks draft without changing protocol negotiation.
    /// </summary>
    /// <remarks>
    /// Call this method when using <see cref="McpTaskMigrationClient"/> with a client whose
    /// <see cref="McpClientOptions.ProtocolVersion"/> remains unset. The client will continue to
    /// prefer the newest protocol revision and use Core's normal fallback behavior.
    /// </remarks>
    public static McpClientOptions EnableTasksMigration(this McpClientOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        AddLegacyTasksCapability(options);
        return options;
    }

    private static void AddLegacyTasksCapability(McpClientOptions options)
    {
        options.Capabilities ??= new();
        options.Capabilities.AdditionalProperties ??= new Dictionary<string, JsonElement>();
        options.Capabilities.AdditionalProperties[LegacyTasksProtocol.CapabilityName] = CreateCapabilityElement();
    }

    /// <summary>Calls a tool and returns either its immediate result or a created legacy task.</summary>
    public static async ValueTask<LegacyTaskCallResult> CallToolAsLegacyTaskAsync(
        this McpClient client,
        CallToolRequestParams requestParams,
        McpLegacyTaskMetadata? taskMetadata = null,
        CancellationToken cancellationToken = default)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (requestParams is null)
        {
            throw new ArgumentNullException(nameof(requestParams));
        }

        ThrowIfLegacyTasksUnavailable(client, nameof(CallToolAsLegacyTaskAsync));

        JsonObject parameters = new()
        {
            ["name"] = requestParams.Name,
            [LegacyTasksProtocol.TaskPropertyName] = JsonSerializer.SerializeToNode(
                taskMetadata ?? new McpLegacyTaskMetadata(),
                LegacyTasksJsonContext.Default.McpLegacyTaskMetadata),
        };

        if (requestParams.Arguments is not null)
        {
            parameters["arguments"] = JsonSerializer.SerializeToNode(
                requestParams.Arguments,
                McpJsonUtilities.DefaultOptions.GetTypeInfo<IDictionary<string, JsonElement>>());
        }

        if (requestParams.Meta is not null)
        {
            parameters["_meta"] = requestParams.Meta.DeepClone();
        }

        JsonRpcResponse response = await client.SendRequestAsync(
            new JsonRpcRequest
            {
                Method = RequestMethods.ToolsCall,
                Params = parameters,
            },
            cancellationToken).ConfigureAwait(false);

        if (response.Result is JsonObject result && result.ContainsKey(LegacyTasksProtocol.TaskPropertyName))
        {
            var created = result.Deserialize(LegacyTasksJsonContext.Default.CreateLegacyTaskResult)
                ?? throw new JsonException("The legacy task creation response was empty.");
            return new LegacyTaskCallResult(created.Task);
        }

        var callResult = JsonSerializer.Deserialize(
            response.Result,
            McpJsonUtilities.DefaultOptions.GetTypeInfo<CallToolResult>())
            ?? throw new JsonException("The tools/call response was empty.");
        return new LegacyTaskCallResult(callResult);
    }

    /// <summary>Gets the status of a legacy task.</summary>
    public static ValueTask<GetLegacyTaskResult> GetLegacyTaskAsync(
        this McpClient client,
        string taskId,
        CancellationToken cancellationToken = default)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (taskId is null)
        {
            throw new ArgumentNullException(nameof(taskId));
        }

        ThrowIfLegacyTasksUnavailable(client, nameof(GetLegacyTaskAsync));

        return client.SendRequestAsync<GetLegacyTaskRequestParams, GetLegacyTaskResult>(
            LegacyTasksProtocol.GetTaskMethod,
            new GetLegacyTaskRequestParams { TaskId = taskId },
            LegacyTasksJsonContext.Default.Options,
            cancellationToken: cancellationToken);
    }

    /// <summary>Lists legacy tasks exposed by the connected server.</summary>
    public static ValueTask<ListLegacyTasksResult> ListLegacyTasksAsync(
        this McpClient client,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        ThrowIfLegacyTasksUnavailable(client, nameof(ListLegacyTasksAsync));

        return client.SendRequestAsync<ListLegacyTasksRequestParams, ListLegacyTasksResult>(
            LegacyTasksProtocol.ListTasksMethod,
            new ListLegacyTasksRequestParams { Cursor = cursor },
            LegacyTasksJsonContext.Default.Options,
            cancellationToken: cancellationToken);
    }

    /// <summary>Waits for and gets the raw payload of a terminal legacy task.</summary>
    public static async ValueTask<JsonElement> GetLegacyTaskPayloadAsync(
        this McpClient client,
        string taskId,
        CancellationToken cancellationToken = default)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (taskId is null)
        {
            throw new ArgumentNullException(nameof(taskId));
        }

        ThrowIfLegacyTasksUnavailable(client, nameof(GetLegacyTaskPayloadAsync));

        JsonRpcResponse response = await client.SendRequestAsync(
            new JsonRpcRequest
            {
                Method = LegacyTasksProtocol.GetTaskResultMethod,
                Params = JsonSerializer.SerializeToNode(
                    new GetLegacyTaskPayloadRequestParams { TaskId = taskId },
                    LegacyTasksJsonContext.Default.GetLegacyTaskPayloadRequestParams),
            },
            cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(response.Result?.ToJsonString() ?? "null");
        return document.RootElement.Clone();
    }

    /// <summary>Cancels a legacy task.</summary>
    public static ValueTask<CancelLegacyTaskResult> CancelLegacyTaskAsync(
        this McpClient client,
        string taskId,
        CancellationToken cancellationToken = default)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (taskId is null)
        {
            throw new ArgumentNullException(nameof(taskId));
        }

        ThrowIfLegacyTasksUnavailable(client, nameof(CancelLegacyTaskAsync));

        return client.SendRequestAsync<CancelLegacyTaskRequestParams, CancelLegacyTaskResult>(
            LegacyTasksProtocol.CancelTaskMethod,
            new CancelLegacyTaskRequestParams { TaskId = taskId },
            LegacyTasksJsonContext.Default.Options,
            cancellationToken: cancellationToken);
    }

    private static JsonElement CreateCapabilityElement() =>
        JsonSerializer.SerializeToElement(
            new JsonObject
            {
                ["list"] = new JsonObject(),
                ["cancel"] = new JsonObject(),
            },
            McpJsonUtilities.DefaultOptions.GetTypeInfo<JsonNode>());

    private static void ThrowIfLegacyTasksUnavailable(McpClient client, string operation)
    {
        if (!string.Equals(client.NegotiatedProtocolVersion, LegacyTasksProtocol.ProtocolVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"'{operation}' requires negotiated protocol version '{LegacyTasksProtocol.ProtocolVersion}', " +
                $"but the connected server negotiated '{client.NegotiatedProtocolVersion ?? "(none)"}'.");
        }

        if (client.ServerCapabilities.AdditionalProperties?.ContainsKey(LegacyTasksProtocol.CapabilityName) is not true)
        {
            throw new InvalidOperationException("The connected server does not advertise legacy Tasks support.");
        }
    }
}

/// <summary>Represents either an immediate tool result or a created legacy task.</summary>
public sealed class LegacyTaskCallResult
{
    internal LegacyTaskCallResult(CallToolResult result) => Result = result;

    internal LegacyTaskCallResult(McpLegacyTask task) => Task = task;

    /// <summary>Gets whether the server created a task.</summary>
    public bool IsTask => Task is not null;

    /// <summary>Gets the immediate tool result when <see cref="IsTask"/> is false.</summary>
    public CallToolResult? Result { get; }

    /// <summary>Gets the created task when <see cref="IsTask"/> is true.</summary>
    public McpLegacyTask? Task { get; }
}
