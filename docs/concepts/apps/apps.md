---
title: MCP Apps
author: mikekistler
description: How to use the MCP Apps extension to deliver interactive UIs from MCP servers.
uid: apps
---

# MCP Apps

[MCP Apps] is an extension to the Model Context Protocol that enables MCP servers to deliver interactive user interfaces — dashboards, forms, visualizations, and more — directly inside conversational AI clients.

[MCP Apps]: https://modelcontextprotocol.io/extensions/apps/overview

> [!IMPORTANT]
> MCP Apps support is experimental. All types are marked with `[Experimental("MCPEXP003")]` and require suppressing that diagnostic to use.

## Installation

MCP Apps is provided in the `ModelContextProtocol.Extensions.Apps` package, which layers on top of the core SDK:

```shell
dotnet add package ModelContextProtocol.Extensions.Apps
```

## Overview

The MCP Apps extension introduces the concept of **UI resources** — HTML pages served by the MCP server that a client can display alongside the conversation. Tools can be associated with a UI resource so the client knows which interface to show when a tool is called.

The key concepts are:

- **UI capability negotiation** — Client and server declare support via `extensions["io.modelcontextprotocol/ui"]`
- **UI resources** — HTML content served with the MIME type `text/html;profile=mcp-app`
- **Tool UI metadata** — Tools declare their associated UI resource in `_meta.ui`

## Associating tools with UI resources

### Using the builder extension (recommended)

The simplest approach is to apply `[McpAppUi]` attributes to your tool methods and call `WithMcpApps()` on the server builder:

[!code-csharp[](Apps.cs?name=snippet_AppsWeatherTools)]

[!code-csharp[](Apps.cs?name=snippet_AppsWithMcpApps)]

The `WithMcpApps()` call registers a post-configuration step that processes all registered tools and applies `[McpAppUi]` attribute metadata to their `_meta.ui` field automatically.

### Using the attribute with manual processing

If you create tools manually (without `WithMcpApps()`), you can still use the attribute and process tools explicitly:

[!code-csharp[](Apps.cs?name=snippet_AppsManualProcessing)]

### Using the programmatic API

For full control, use `McpApps.SetAppUi` to set UI metadata directly:

[!code-csharp[](Apps.cs?name=snippet_AppsSetAppUi)]

## Checking client capabilities

During a session, you can check whether the connected client supports MCP Apps:

[!code-csharp[](Apps.cs?name=snippet_AppsCheckCapability)]

## Tool visibility

The `Visibility` property controls which principals can invoke the tool:

| Value                       | Meaning                                            |
|-----------------------------|----------------------------------------------------|
| `McpUiToolVisibility.Model` | Only the LLM can call this tool                    |
| `McpUiToolVisibility.App`   | Only the app UI can call this tool                 |
| Both (or null/empty)        | Both the model and app can call the tool (default) |

## UI resources

UI resources are HTML pages registered with the MCP server using the `ui://` URI scheme and the `text/html;profile=mcp-app` MIME type. The `McpUiResourceMeta` type provides metadata for these resources, including:

- **CSP (Content Security Policy)** — Controls allowed origins for network requests and resource loads
- **Permissions** — Sandbox permissions (scripts, forms, popups, etc.)
- **Domain** — Dedicated origin for OAuth flows and CORS
- **PrefersBorder** — Whether the host should render a visual border

## App-only tools

Tools with `Visibility = [McpUiToolVisibility.App]` are not visible to the LLM — they are intended only for use by the app UI.
This is useful for tools that serve UI interaction (button handlers, form submissions) without cluttering the model's tool list:

[!code-csharp[](Apps.cs?name=snippet_AppsAppOnly)]

## Graceful degradation

Not all clients support MCP Apps. Use `GetUiCapability` to detect support and return text-only content as a fallback:

[!code-csharp[](Apps.cs?name=snippet_AppsGracefulDegradation)]

## Display modes

The MCP Apps spec defines display modes (`inline`, `fullscreen`, `pip`) that control how the host renders the UI. Display mode is negotiated between the client and server during capability exchange and is not set per-tool — it depends on the host implementation.

## Host theming

Hosts pass standardized CSS custom properties (for example, `--color-background-primary`, `--color-text-primary`) to app iframes. Your HTML can reference these variables to automatically match the host's theme without any server-side configuration.

See the [MCP Apps specification](https://github.com/modelcontextprotocol/ext-apps/blob/main/specification/draft/apps.mdx) for the full list of CSS variables.

## Single-file HTML bundling

The default Content Security Policy restricts external script and style loads. For production apps, bundle all JavaScript and CSS into a single HTML file using tools like [vite-plugin-singlefile](https://github.com/richardtallent/vite-plugin-singlefile). For simple apps, inline `<script>` and `<style>` tags work directly.

## Constants

The <xref:ModelContextProtocol.Extensions.Apps.McpApps> class provides constants for protocol values:

| Constant               | Value                        | Usage                                     |
|------------------------|------------------------------|-------------------------------------------|
| `McpApps.HtmlMimeType` | `text/html;profile=mcp-app`  | MIME type for UI resources                |
| `McpApps.ExtensionId`  | `io.modelcontextprotocol/ui` | Key in `extensions` capability dictionary |

## Serialization

MCP Apps types use source-generated JSON serialization for Native AOT compatibility. Use `McpApps.SerializerOptions` when serializing extension types:

[!code-csharp[](Apps.cs?name=snippet_AppsSerialization)]
