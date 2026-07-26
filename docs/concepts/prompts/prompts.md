---
title: Prompts
author: jeffhandley
description: How to implement and consume MCP prompts that return text, images, and embedded resources.
uid: prompts
---

## Prompts

MCP [prompts] allow servers to expose reusable prompt templates to clients. Prompts provide a way for servers to define structured messages that can be parameterized and composed into conversations.

[prompts]: https://modelcontextprotocol.io/specification/2025-11-25/server/prompts

This document covers implementing prompts on the server, consuming prompts from the client, rich content types, and change notifications.

### Defining prompts on the server

Prompts can be defined in several ways:

- Using the <xref:ModelContextProtocol.Server.McpServerPromptAttribute> attribute on methods within a class marked with <xref:ModelContextProtocol.Server.McpServerPromptTypeAttribute>
- Using <xref:ModelContextProtocol.Server.McpServerPrompt.Create*> factory methods from a delegate, `MethodInfo`, or `AIFunction`
- Deriving from <xref:ModelContextProtocol.Server.McpServerPrompt> or <xref:ModelContextProtocol.Server.DelegatingMcpServerPrompt>
- Implementing a custom <xref:ModelContextProtocol.Server.McpRequestHandler`2> via <xref:ModelContextProtocol.Server.McpServerHandlers>
- Implementing a low-level <xref:ModelContextProtocol.Server.McpRequestFilter`2>

The attribute-based approach is the most common and is shown throughout this document. Prompts can return `ChatMessage` instances for simple text/image content, or <xref:ModelContextProtocol.Protocol.PromptMessage> instances when protocol-specific content types like <xref:ModelContextProtocol.Protocol.EmbeddedResourceBlock> are needed.

#### Simple prompts

A prompt without arguments:

[!code-csharp[](Prompts.cs?name=snippet_PromptsSimple)]

#### Prompts with arguments

Prompts can accept parameters to customize the generated messages. Use `[Description]` attributes to document each parameter. In addition to prompt arguments, methods can accept special parameter types that are resolved automatically: <xref:ModelContextProtocol.Server.McpServer>, `IProgress<ProgressNotificationValue>`, `ClaimsPrincipal`, and any service registered through dependency injection.

[!code-csharp[](Prompts.cs?name=snippet_PromptsWithArgs)]

Register prompt types when building the server:

[!code-csharp[](Prompts.cs?name=snippet_PromptsRegister)]

### Rich content in prompts

Prompt messages can contain more than just text. For text and image content, use [`ChatMessage`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.chatmessage) from Microsoft.Extensions.AI. `DataContent` is automatically mapped to the appropriate MCP content block: image MIME types become <xref:ModelContextProtocol.Protocol.ImageContentBlock>, audio MIME types become <xref:ModelContextProtocol.Protocol.AudioContentBlock>, and all other MIME types become <xref:ModelContextProtocol.Protocol.EmbeddedResourceBlock> with binary resource contents. For text-embedded resources specifically, use <xref:ModelContextProtocol.Protocol.PromptMessage> directly.

#### Image content

Include images in prompts using `DataContent`:

[!code-csharp[](Prompts.cs?name=snippet_PromptsImage)]

#### Embedded resources

For protocol-specific content types like <xref:ModelContextProtocol.Protocol.EmbeddedResourceBlock>, use <xref:ModelContextProtocol.Protocol.PromptMessage> instead of `ChatMessage`. `PromptMessage` has a `Role` property and a single `Content` property of type <xref:ModelContextProtocol.Protocol.ContentBlock>:

[!code-csharp[](Prompts.cs?name=snippet_PromptsEmbedded)]

For binary resources, use the <xref:ModelContextProtocol.Protocol.BlobResourceContents.FromBytes*> factory method:

[!code-csharp[](Prompts.cs?name=snippet_PromptsBlob)]

### Consuming prompts on the client

Clients can discover and use prompts through <xref:ModelContextProtocol.Client.McpClient>.

#### Listing prompts

[!code-csharp[](Prompts.cs?name=snippet_PromptsList)]

#### Getting a prompt

[!code-csharp[](Prompts.cs?name=snippet_PromptsGet)]

### Prompt list change notifications

Servers can dynamically add, remove, or modify prompts at runtime and notify connected clients. These are unsolicited notifications, so they require [stateful mode or stdio](xref:stateless) — [stateless](xref:stateless#stateless-mode-recommended) servers cannot send unsolicited notifications.

#### Sending notifications from the server

[!code-csharp[](Prompts.cs?name=snippet_PromptsNotify)]

#### Handling notifications on the client

[!code-csharp[](Prompts.cs?name=snippet_PromptsHandle)]
