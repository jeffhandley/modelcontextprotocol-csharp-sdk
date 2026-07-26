---
title: Filters
author: halter73
description: MCP Server Filters
uid: filters
---

# MCP Server Filters

The MCP Server provides two levels of filters for intercepting and modifying request processing:

1. **Message Filters** - Low-level filters (`AddIncomingFilter`, `AddOutgoingFilter`) configured via `WithMessageFilters(...)` that intercept all JSON-RPC messages before routing.
2. **Request-Specific Filters** - Handler-level filters (for example, `AddListToolsFilter`, `AddCallToolFilter`) configured via `WithRequestFilters(...)` that target specific MCP operations.

The filters are stored in `McpServerOptions.Filters`.

## Available request-specific filter methods

The following request filter methods are available on `IMcpRequestFilterBuilder` inside `WithRequestFilters(...)`:

| Method                              | Filters for...                   |
|-------------------------------------|----------------------------------|
| `AddListResourceTemplatesFilter`    | List resource templates handlers |
| `AddListToolsFilter`                | List tools handlers              |
| `AddCallToolFilter`                 | Call tool handlers               |
| `AddListPromptsFilter`              | List prompts handlers            |
| `AddGetPromptFilter`                | Get prompt handlers              |
| `AddListResourcesFilter`            | List resources handlers          |
| `AddReadResourceFilter`             | Read resource handlers           |
| `AddCompleteFilter`                 | Completion handlers              |
| `AddSubscribeToResourcesFilter`     | Resource subscription handlers   |
| `AddUnsubscribeFromResourcesFilter` | Resource unsubscription handlers |
| `AddSetLoggingLevelFilter`          | Logging level handlers           |

The Tasks extension and ASP.NET Core tool authorization use alternate-result `tools/call` filters. Alternate-result filters run in registration order outside the ordinary `AddCallToolFilter` pipeline. The Tasks filter creates a task at its position in that order and runs its remaining pipeline in the background. Consequently, alternate-result filters registered before Tasks run before task creation, while those registered after Tasks run in the background before the ordinary filters and tool. ASP.NET Core authorization is registered before Tasks, so an unauthorized call is rejected without creating a task.

Ordinary call-tool filters run exactly once after all alternate-result filters and before the tool. For task-backed calls, ordinary filters execute in the background after task creation. An explicit `CallToolWithAlternateHandler` remains a full replacement and cannot be combined with ordinary call-tool filters.

Configure `WithTasks` before adding ordinary call-tool filters. Tasks validates this ordering when its filter is installed because it must execute outside the ordinary call-tool pipeline.

## Message filters

In addition to the request-specific filters above, there are low-level message filters that intercept all JSON-RPC messages before they are routed to specific handlers.
Configure these on `IMcpMessageFilterBuilder` inside `WithMessageFilters(...)`:

- `AddIncomingFilter` - Filter for all incoming JSON-RPC messages (requests and notifications)
- `AddOutgoingFilter` - Filter for all outgoing JSON-RPC messages (responses and notifications)

### When to use message filters

Message filters operate at a lower level than request-specific filters and are useful when you need to:

- Intercept all messages regardless of type
- Implement custom protocol extensions or handle custom JSON-RPC methods
- Log or monitor all traffic between client and server
- Modify or skip messages before they reach handlers
- Send additional messages in response to specific events

### Incoming message filter

`AddIncomingFilter` intercepts all incoming JSON-RPC messages before they are dispatched to request-specific handlers:

[!code-csharp[](Filters.cs?name=snippet_FiltersIncoming)]

#### MessageContext Properties

Inside an incoming message filter, you have access to:

- `context.JsonRpcMessage` - The incoming `JsonRpcMessage` (can be `JsonRpcRequest` or `JsonRpcNotification`)
- `context.Server` - The `McpServer` instance for sending responses or notifications
- `context.Services` - The request's service provider
- `context.Items` - A dictionary for passing data between filters

#### Skipping default handlers

You can skip the default handler by not calling `next`. This is useful for implementing custom protocol methods:

[!code-csharp[](Filters.cs?name=snippet_FiltersSkipDefault)]

### Outgoing message filter

`AddOutgoingFilter` intercepts all outgoing JSON-RPC messages before they are sent to the client:

[!code-csharp[](Filters.cs?name=snippet_FiltersOutgoing)]

#### Skipping outgoing messages

You can suppress outgoing messages by not calling `next`:

[!code-csharp[](Filters.cs?name=snippet_FiltersSkipOutgoing)]

#### Sending additional messages

Outgoing message filters can send additional messages by calling `next` with a new `MessageContext`:

[!code-csharp[](Filters.cs?name=snippet_FiltersSendAdditional)]

### Message filter execution order

Message filters execute in registration order, with the first registered filter being the outermost:

[!code-csharp[](Filters.cs?name=snippet_FiltersOrder)]

**Important**: Incoming message filters always run before request-specific filters, and outgoing message filters run when responses or notifications are sent. The complete execution flow for a request/response cycle is:

```
Request arrives
    ↓
IncomingFilter1 (before next)
    ↓
IncomingFilter2 (before next)
    ↓
Request Routing → ListToolsFilter → Handler
    ↓
IncomingFilter2 (after next)
    ↓
IncomingFilter1 (after next)
    ↓
Response sent via OutgoingFilter1 (before next)
    ↓
OutgoingFilter2 (before next)
    ↓
Transport sends message
    ↓
OutgoingFilter2 (after next)
    ↓
OutgoingFilter1 (after next)
```

### Passing data between filters

The `Items` dictionary allows you to pass data between filters processing the same message:

[!code-csharp[](Filters.cs?name=snippet_FiltersPassingData)]

## Usage

Filters are functions that take a handler and return a new handler, allowing you to wrap the original handler with additional functionality:

[!code-csharp[](Filters.cs?name=snippet_FiltersUsage)]

## Filter execution order

[!code-csharp[](Filters.cs?name=snippet_FiltersRequestOrder)]

Execution flow: `filter1 -> filter2 -> filter3 -> baseHandler -> filter3 -> filter2 -> filter1`

## Common use cases

Filters are commonly used for [logging](#logging), [error handling](#error-handling), [performance monitoring](#performance-monitoring), and [caching](#caching).

### Logging

[!code-csharp[](Filters.cs?name=snippet_FiltersLogging)]

### Error handling

[!code-csharp[](Filters.cs?name=snippet_FiltersErrorHandling)]

### Performance monitoring

[!code-csharp[](Filters.cs?name=snippet_FiltersPerformance)]

### Caching

[!code-csharp[](Filters.cs?name=snippet_FiltersCaching)]

## Built-in authorization request filters

When using the ASP.NET Core integration (`ModelContextProtocol.AspNetCore`), you can add authorization filters to support `[Authorize]` and `[AllowAnonymous]` attributes on MCP server tools, prompts, and resources by calling `AddAuthorizationFilters()` on your MCP server builder.

### Enabling authorization request filters

To enable authorization support, call `AddAuthorizationFilters()` when configuring your MCP server:

[!code-csharp[](Filters.cs?name=snippet_FiltersAuthEnable)]

**Important**: If you want to use authorization attributes like `[Authorize]` on your MCP server tools, prompts, or resources, you should always call `AddAuthorizationFilters()` when using ASP.NET Core integration.

### Authorization attributes support

The MCP server automatically respects the following authorization attributes:

- **`[Authorize]`** - Requires authentication for access
- **`[Authorize(Roles = "RoleName")]`** - Requires specific roles
- **`[Authorize(Policy = "PolicyName")]`** - Requires specific authorization policies
- **`[AllowAnonymous]`** - Explicitly allows anonymous access (overrides `[Authorize]`)

### Tool authorization

Tools can be decorated with authorization attributes to control access:

[!code-csharp[](Filters.cs?name=snippet_FiltersWeatherTools)]

### Class-level authorization

You can apply authorization at the class level, which affects all tools in the class:

[!code-csharp[](Filters.cs?name=snippet_FiltersClassAuth)]

### How authorization filters work

The authorization filters work differently for list operations versus individual operations:

#### List operations (`ListTools`, `ListPrompts`, `ListResources`)

For list operations, the filters automatically remove unauthorized items from the results. Users only see tools, prompts, or resources they have permission to access.

#### Individual operations (`CallTool`, `GetPrompt`, `ReadResource`)

For individual operations, the filters throw an `McpException` with "Access forbidden" message. These get turned into JSON-RPC errors if uncaught by middleware.

### Filter execution order and authorization

Authorization filters are applied automatically when you call `AddAuthorizationFilters()`. These filters run at a specific point in the filter pipeline, which means:

**Filters added before authorization filters** can see:

- Unauthorized requests for operations before they are rejected by the authorization filters.
- Complete listings for unauthorized primitives before they are filtered out by the authorization filters.

**Filters added after authorization filters** will only see:

- Authorized requests that passed authorization checks.
- Filtered listings containing only authorized primitives.

This allows you to implement logging, metrics, or other cross-cutting concerns that need to see all requests, while still maintaining proper authorization:

[!code-csharp[](Filters.cs?name=snippet_FiltersAuthOrder)]

### Setup requirements

To use authorization features, you must configure authentication and authorization in your ASP.NET Core application and call `AddAuthorizationFilters()`:

[!code-csharp[](Filters.cs?name=snippet_FiltersSetup)]

### Custom authorization filters

You can also create custom authorization filters using the filter methods:

[!code-csharp[](Filters.cs?name=snippet_FiltersCustomAuth)]

### RequestContext

Within filters, you have access to:

- `context.User` - The current user's `ClaimsPrincipal`.
- `context.Services` - The request's service provider for resolving authorization services.
- `context.MatchedPrimitive` - The matched tool/prompt/resource with its metadata including authorization attributes via `context.MatchedPrimitive.Metadata`.
