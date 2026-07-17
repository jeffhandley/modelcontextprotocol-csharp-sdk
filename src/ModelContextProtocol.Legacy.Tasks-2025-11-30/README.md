# ModelContextProtocol.Legacy.Tasks-2025-11-30

This proof-of-concept package adds server and client support for the experimental MCP Tasks draft negotiated as `2025-11-30`. It is intended only for compatibility with peers that implemented that draft.

Use `WithLegacyTasks` to enable server-side support. The package can be referenced together with `ModelContextProtocol.Extensions.Tasks`; the negotiated protocol version selects which implementation handles a request.

```csharp
builder
    .WithLegacyTasks(new InMemoryLegacyMcpTaskStore())
    .WithTasks(new InMemoryMcpTaskStore());

var clientOptions = new McpClientOptions().EnableTasksMigration();
await using var client = await McpClient.CreateAsync(transport, clientOptions);
var tasks = client.CreateTaskMigrationClient(logger);

CallToolResult result = await tasks.CallToolWithPollingAsync(
    new() { Name = "long-running-tool" });
```

`EnableTasksMigration` advertises the legacy top-level `tasks` capability but does not set `McpClientOptions.ProtocolVersion`. The SDK therefore uses its standard protocol negotiation and fallback behavior. Create `McpTaskMigrationClient` only after connection: it delegates to current Tasks for `2026-07-28` or later and to legacy Tasks for exactly `2025-11-30`. Its optional logger emits an Information-level record naming every server for which legacy Tasks is selected.

For a single execution pipeline, call `McpTaskMigrationClient.ExecuteToolAsync` with the `McpClientTool` returned by `ListToolsAsync` and its request parameters. On a legacy connection it enters the task/polling path only when the tool metadata contains:

```json
"execution": {
  "taskSupport": "optional"
}
```

Tools without that property use the ordinary `tools/call` path. On a current connection, the server-level `io.modelcontextprotocol/tasks` extension capability indicates task support.

`EnableLegacyTasks` remains available for clients that intentionally pin themselves to `2025-11-30`. It pins the protocol version and is not the migration path.

The proof of concept covers task-augmented `tools/call` requests and the `tasks/get`, `tasks/list`, `tasks/result`, and `tasks/cancel` lifecycle. It does not recreate the draft's reverse-direction sampling or elicitation task augmentation.
