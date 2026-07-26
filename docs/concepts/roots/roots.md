---
title: Roots
author: jeffhandley
description: How to provide filesystem roots from clients to MCP servers.
uid: roots
---

## Roots

MCP [roots] allow clients to inform servers about the relevant locations in the filesystem or other hierarchical data sources. Roots are a client-provided feature&mdash;the client declares its root URIs during initialization, and the server can request them to understand the working context.

[roots]: https://modelcontextprotocol.io/specification/2025-11-25/client/roots

> [!IMPORTANT]
> Roots are **deprecated** as of MCP specification revision `2026-07-28` ([SEP-2577](https://github.com/modelcontextprotocol/modelcontextprotocol/pull/2577), [MCP9005](xref:list-of-diagnostics#obsolete-apis)) and may be removed in a future version.

### Overview

Roots provide a mechanism for the client to tell the server which directories, projects, or repositories are relevant to the current session. A server might use roots to:

- Scope file searches to the user's project directories
- Understand which repositories are being worked on
- Focus operations on relevant filesystem locations

Each root is represented by a <xref:ModelContextProtocol.Protocol.Root> with a URI and an optional human-readable name.

### Declaring roots capability on the client

Clients advertise their support for roots in the capabilities sent during initialization. The roots capability is created automatically when a roots handler is provided. Configure the handler through <xref:ModelContextProtocol.Client.McpClientHandlers.RootsHandler>:

[!code-csharp[](Roots.cs?name=snippet_RootsHandler)]

### Requesting roots from the server

Servers can request the client's root list using <xref:ModelContextProtocol.Server.McpServer.RequestRootsAsync*>. This is a server-to-client request, so it requires [stateful mode or stdio](xref:stateless) — it is not available in [stateless mode](xref:stateless#stateless-mode-recommended). For a stateless-compatible alternative, throw `InputRequiredException` from your handler through MRTR — see [Multi round-trip requests (MRTR)](#multi-round-trip-requests-mrtr).

[!code-csharp[](Roots.cs?name=snippet_RequestRoots)]

### Roots change notifications

When the set of roots changes (for example, the user opens a new project), the client notifies the server so it can update its understanding of the working context.

#### Sending change notifications from the client

Roots change notifications are automatically sent when the client's roots handler is updated. However, clients can also send the notification explicitly:

[!code-csharp[](Roots.cs?name=snippet_RootsListChanged)]

#### Handling change notifications on the server

Servers can register a handler to respond when the client's roots change:

[!code-csharp[](Roots.cs?name=snippet_RootsChangeHandler)]

### Multi round-trip requests (MRTR)

[MRTR](xref:mrtr) is the SEP-2322 mechanism for server-driven input requests, finalized in protocol revision `2026-07-28`. In that revision, the server-to-client `roots/list` request method is removed; the recommended way to ask the client for its roots from a server handler is to throw <xref:ModelContextProtocol.Protocol.InputRequiredException> and let the SDK emit an <xref:ModelContextProtocol.Protocol.InputRequiredResult> on the wire.

> [!IMPORTANT]
> `RequestRootsAsync` throws `InvalidOperationException("Roots are not supported in stateless mode.")` whenever the server is running stateless — including Streamable HTTP requests served under `2026-07-28` with `Stateless = true`. Stdio servers and initialize-handshake stateful Streamable HTTP sessions continue to work via the initialize-era server-to-client `roots/list` request flow; an HTTP server set to `Stateless = false` refuses `2026-07-28` so dual-path clients can fall back before using that flow. For code that needs to run on stateless servers — including `2026-07-28` Streamable HTTP — throw `InputRequiredException` from your handler instead. It works under both protocols and both session modes.

For example:

[!code-csharp[](Roots.cs?name=snippet_RootsMrtr)]

> [!TIP]
> For the full protocol details, including load shedding, multiple round trips, and the compatibility matrix, see [Multi Round-Trip Requests (MRTR)](xref:mrtr).
