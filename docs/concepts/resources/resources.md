---
title: Resources
author: jeffhandley
description: How to implement and consume MCP resources for exposing data to clients.
uid: resources
---

## Resources

MCP [resources] allow servers to expose data and content to clients. Resources represent any kind of data that a server wants to make available&mdash;files, database records, API responses, live system data, and more.

[resources]: https://modelcontextprotocol.io/specification/2025-11-25/server/resources

This document covers implementing resources on the server, consuming resources from the client, resource templates, subscriptions, and change notifications.

### Defining resources on the server

Resources can be defined in several ways:

- Using the <xref:ModelContextProtocol.Server.McpServerResourceAttribute> attribute on methods within a class marked with <xref:ModelContextProtocol.Server.McpServerResourceTypeAttribute>
- Using <xref:ModelContextProtocol.Server.McpServerResource.Create*> factory methods from a delegate, `MethodInfo`, or `AIFunction`
- Deriving from <xref:ModelContextProtocol.Server.McpServerResource> or <xref:ModelContextProtocol.Server.DelegatingMcpServerResource>
- Implementing a custom <xref:ModelContextProtocol.Server.McpRequestHandler`2> via <xref:ModelContextProtocol.Server.McpServerHandlers>
- Implementing a low-level <xref:ModelContextProtocol.Server.McpRequestFilter`2>

The attribute-based approach is the most common and is shown throughout this document.

#### Direct resources

Direct resources have a fixed URI and are returned in the resource list:

[!code-csharp[](Resources.cs?name=snippet_ResourcesDirect)]

#### Template resources

Template resources use [URI templates (RFC 6570)] with parameters. They are returned separately in the resource templates list and can match a range of URIs:

[URI templates (RFC 6570)]: https://datatracker.ietf.org/doc/html/rfc6570

[!code-csharp[](Resources.cs?name=snippet_ResourcesTemplate)]

Register resource types when building the server:

[!code-csharp[](Resources.cs?name=snippet_ResourcesRegister)]

### Reading text resources

Text resources return their content as <xref:ModelContextProtocol.Protocol.TextResourceContents> with a `Text` property:

[!code-csharp[](Resources.cs?name=snippet_ResourcesText)]

### Reading binary resources

Binary resources return their content as <xref:ModelContextProtocol.Protocol.BlobResourceContents> with a `Blob` property containing the raw bytes. Use the <xref:ModelContextProtocol.Protocol.BlobResourceContents.FromBytes*> factory method to construct instances:

[!code-csharp[](Resources.cs?name=snippet_ResourcesBinary)]

### Consuming resources on the client

Clients can discover and read resources using <xref:ModelContextProtocol.Client.McpClient>:

#### Listing resources

[!code-csharp[](Resources.cs?name=snippet_ResourcesList)]

#### Listing resource templates

[!code-csharp[](Resources.cs?name=snippet_ResourcesListTemplates)]

#### Reading a resource

[!code-csharp[](Resources.cs?name=snippet_ResourcesRead)]

#### Reading a template resource

[!code-csharp[](Resources.cs?name=snippet_ResourcesReadTemplate)]

### Resource subscriptions

Clients can subscribe to resource updates to be notified when a resource's content changes. The server must declare subscription support in its capabilities.

#### Subscribing on the client

[!code-csharp[](Resources.cs?name=snippet_ResourcesSubscribe)]

Clients can also subscribe and unsubscribe separately:

[!code-csharp[](Resources.cs?name=snippet_ResourcesSubscribeSeparate)]

#### Handling subscriptions on the server

Register subscription handlers when building the server:

[!code-csharp[](Resources.cs?name=snippet_ResourcesHandlers)]

#### Sending resource update notifications

When a resource's content changes, the server notifies subscribed clients:

[!code-csharp[](Resources.cs?name=snippet_ResourcesNotifyUpdate)]

### Resource list change notifications

When the set of available resources changes (resources added or removed), the server notifies clients.

#### Sending notifications from the server

[!code-csharp[](Resources.cs?name=snippet_ResourcesNotifyList)]

#### Handling notifications on the client

[!code-csharp[](Resources.cs?name=snippet_ResourcesHandleList)]
