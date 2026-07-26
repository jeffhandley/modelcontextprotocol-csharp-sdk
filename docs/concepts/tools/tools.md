---
title: Tools
author: jeffhandley
description: How to implement and consume MCP tools that return text, images, audio, and embedded resources.
uid: tools
---

## Tools

MCP [tools] allow servers to expose callable functions to clients. Tools are the primary mechanism for LLMs to take action through MCP&mdash;they enable everything from querying databases to calling web APIs.

[tools]: https://modelcontextprotocol.io/specification/2025-11-25/server/tools

This document covers tool content types, change notifications, and schema generation.

### Defining tools on the server

Tools can be defined in several ways:

- Using the <xref:ModelContextProtocol.Server.McpServerToolAttribute> attribute on methods within a class marked with <xref:ModelContextProtocol.Server.McpServerToolTypeAttribute>
- Using <xref:ModelContextProtocol.Server.McpServerTool.Create*> factory methods from a delegate, `MethodInfo`, or `AIFunction`
- Deriving from <xref:ModelContextProtocol.Server.McpServerTool> or <xref:ModelContextProtocol.Server.DelegatingMcpServerTool>
- Implementing a custom <xref:ModelContextProtocol.Server.McpRequestHandler`2> via <xref:ModelContextProtocol.Server.McpServerHandlers>
- Implementing a low-level <xref:ModelContextProtocol.Server.McpRequestFilter`2>

The attribute-based approach is the most common and is shown throughout this document. Parameters are automatically deserialized from JSON and documented using `[Description]` attributes. In addition to tool arguments, methods can accept special parameter types that are resolved automatically: <xref:ModelContextProtocol.Server.McpServer>, `IProgress<ProgressNotificationValue>`, `ClaimsPrincipal`, and any service registered through dependency injection.

[!code-csharp[](Tools.cs?name=snippet_MyTools)]

Register the tool type when building the server:

[!code-csharp[](Tools.cs?name=snippet_RegisterTools)]

### Content types

Tools can return various content types. The simplest is a `string`, which is automatically wrapped in a <xref:ModelContextProtocol.Protocol.TextContentBlock>. For richer content, tools can return one or more <xref:ModelContextProtocol.Protocol.ContentBlock> instances. Tools can also return [`DataContent`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.datacontent) from Microsoft.Extensions.AI, which is automatically mapped to the appropriate MCP content block: image MIME types become <xref:ModelContextProtocol.Protocol.ImageContentBlock>, audio MIME types become <xref:ModelContextProtocol.Protocol.AudioContentBlock>, and all other MIME types become <xref:ModelContextProtocol.Protocol.EmbeddedResourceBlock> with binary resource contents.

#### Text content

Return a `string` or a <xref:ModelContextProtocol.Protocol.TextContentBlock> directly:

[!code-csharp[](Tools.cs?name=snippet_Greet)]

#### Image content

Return an <xref:ModelContextProtocol.Protocol.ImageContentBlock> with base64-encoded image data and a MIME type.
Use the <xref:ModelContextProtocol.Protocol.ImageContentBlock.FromBytes*> factory method or construct the block directly:

[!code-csharp[](Tools.cs?name=snippet_GenerateImage)]

#### Audio content

Return an <xref:ModelContextProtocol.Protocol.AudioContentBlock> with base64-encoded audio data and a MIME type.
The <xref:ModelContextProtocol.Protocol.AudioContentBlock.FromBytes*> factory method encodes the raw bytes automatically:

[!code-csharp[](Tools.cs?name=snippet_Synthesize)]

Supported audio MIME types include `audio/wav`, `audio/mp3`, `audio/ogg`, and others depending on what the client can handle.

#### Embedded resources

Return an <xref:ModelContextProtocol.Protocol.EmbeddedResourceBlock> to embed a resource directly in a tool result.
The resource can contain either text or binary data through <xref:ModelContextProtocol.Protocol.TextResourceContents> or <xref:ModelContextProtocol.Protocol.BlobResourceContents>:

[!code-csharp[](Tools.cs?name=snippet_GetDocument)]

For binary resources, use <xref:ModelContextProtocol.Protocol.BlobResourceContents>:

[!code-csharp[](Tools.cs?name=snippet_GetBinaryData)]

#### Mixed content

Tools can return multiple content blocks by returning `IEnumerable<ContentBlock>`:

[!code-csharp[](Tools.cs?name=snippet_DescribeImage)]

#### Content annotations

Any content block can include <xref:ModelContextProtocol.Protocol.Annotations> to provide hints about the intended audience and priority:

[!code-csharp[](Tools.cs?name=snippet_Annotations)]

### Consuming tools on the client

Clients can discover and call tools using <xref:ModelContextProtocol.Client.McpClient>:

[!code-csharp[](Tools.cs?name=snippet_ConsumeTools)]

### Error handling

Tool errors in MCP are distinct from protocol errors. When a tool encounters an error during execution, the error is reported inside the <xref:ModelContextProtocol.Protocol.CallToolResult> with <xref:ModelContextProtocol.Protocol.CallToolResult.IsError> set to `true`, rather than as a protocol-level exception. This allows the LLM to see the error and potentially recover.

#### Automatic exception handling

When a tool method throws an exception, the server catches it and returns a `CallToolResult` with `IsError = true`, with the following exceptions:

- <xref:ModelContextProtocol.McpProtocolException> is re-thrown as a JSON-RPC error response (not a tool error result).
- `OperationCanceledException` is re-thrown when the cancellation token was triggered.

For all other exceptions, the error is returned as a tool result. If the exception derives from <xref:ModelContextProtocol.McpException> (excluding `McpProtocolException`, which is re-thrown above), its message is included in the error text; otherwise, a generic message is returned to avoid leaking internal details.

[!code-csharp[](Tools.cs?name=snippet_Divide)]

#### Protocol errors

Throw <xref:ModelContextProtocol.McpProtocolException> to signal a protocol-level error (for example, invalid parameters or unknown tool). These exceptions propagate as JSON-RPC error responses rather than tool error results:

[!code-csharp[](Tools.cs?name=snippet_Process)]

#### Checking for errors on the client

On the client side, inspect the <xref:ModelContextProtocol.Protocol.CallToolResult.IsError> property after calling a tool:

[!code-csharp[](Tools.cs?name=snippet_CheckErrors)]

### Tool list change notifications

Servers can dynamically add, remove, or modify tools at runtime. When the tool list changes, the server notifies connected clients so they can refresh their tool list. These are unsolicited notifications, so they require [stateful mode or stdio](xref:stateless) — [stateless](xref:stateless#stateless-mode-recommended) servers cannot send unsolicited notifications.

#### Sending notifications from the server

Inject <xref:ModelContextProtocol.Server.McpServer> and call the notification method after modifying the tool list:

[!code-csharp[](Tools.cs?name=snippet_SendToolListChanged)]

#### Handling notifications on the client

Register a notification handler on the client to respond to tool list changes:

[!code-csharp[](Tools.cs?name=snippet_HandleToolListChanged)]

### JSON Schema generation

Tool parameters are described using [JSON Schema 2020-12]. JSON schemas are automatically generated from .NET method signatures when the `[McpServerTool]` attribute is applied. Parameter types are mapped to JSON Schema types:

[JSON Schema 2020-12]: https://json-schema.org/specification

| .NET type         | JSON schema type           |
|-------------------|----------------------------|
| `string`          | `string`                   |
| `int`, `long`     | `integer`                  |
| `float`, `double` | `number`                   |
| `bool`            | `boolean`                  |
| Complex types     | `object` with `properties` |

Use `[Description]` attributes on parameters to populate the `description` field in the generated schema. This helps LLMs understand what each parameter expects.

[!code-csharp[](Tools.cs?name=snippet_Search)]

### Custom HTTP headers from tool parameters

When using the Streamable HTTP transport, tool parameters can be mirrored as HTTP headers so that network infrastructure (load balancers, proxies, gateways) can make routing decisions without parsing the JSON-RPC request body. Apply the <xref:ModelContextProtocol.Server.McpHeaderAttribute> to a parameter to opt it in:

[!code-csharp[](Tools.cs?name=snippet_ExecuteSql)]

When the tool's schema is generated, the annotated parameter includes an `x-mcp-header` extension property. Clients read this annotation and automatically add the corresponding `Mcp-Param-{Name}` header on outgoing `tools/call` requests. The server validates that the header value matches the value in the JSON-RPC body.

Rules and constraints:

- Only primitive parameter types (`string`, numeric types, `bool`) are supported.
- The header name must contain only visible ASCII characters (0x21–0x7E) excluding colon (`:`).
- Values containing non-ASCII characters, control characters, or leading/trailing whitespace are Base64-encoded using the `=?base64?{value}?=` wrapper.
- Header names must be case-insensitively unique within the tool's input schema.
- Header validation is enforced only for protocol versions that support the HTTP Standardization feature (`2026-07-28` and later).

### Pre-loading tool definitions on the client

By default, `Mcp-Param-*` headers are sent only for tools discovered via <xref:ModelContextProtocol.Client.McpClient.ListToolsAsync*>. If a client already has tool schema information (for example, from a previous session, hardcoded configuration, or an out-of-band source), it can pre-load those definitions so that headers are sent immediately—without a round trip to the server.

[!code-csharp[](Tools.cs?name=snippet_PreloadKnownTools)]

Known tools survive <xref:ModelContextProtocol.Client.McpClient.ListToolsAsync*> cache clears—they remain in the cache even when the server's tool list is refreshed. If the server returns a tool with the same name, the server's definition overwrites the cached one, but the tool keeps its known status.

To remove known tools, use <xref:ModelContextProtocol.Client.McpClient.RemoveKnownTools*> for specific tools or <xref:ModelContextProtocol.Client.McpClient.ClearKnownTools*> to remove all:

[!code-csharp[](Tools.cs?name=snippet_RemoveKnownTools)]

All tools passed to <xref:ModelContextProtocol.Client.McpClient.AddKnownTools*> are validated for correct `x-mcp-header` annotations. If any tool in the batch fails validation, an <xref:System.ArgumentException> is thrown and no tools are added (all-or-nothing).
