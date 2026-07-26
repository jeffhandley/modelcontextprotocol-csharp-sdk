---
title: Elicitation
author: mikekistler
description: Enable interactive AI experiences by requesting user input during tool execution.
uid: elicitation
---

## Elicitation

The **elicitation** feature allows servers to request additional information from users during interactions. This feature enables more dynamic and interactive AI experiences, making it easier to gather necessary context before executing tasks.

The protocol supports two modes of elicitation:

- **Form (In-Band)**: The server requests structured data (strings, numbers, Booleans, enums) which the client collects via a form interface and returns to the server.
- **URL Mode**: The server provides a URL for the user to visit (for example, for OAuth, payments, or sensitive data entry). The interaction happens outside the MCP client.

### Server support for elicitation

Servers request information from users with the <xref:ModelContextProtocol.Server.McpServer.ElicitAsync*> extension method on <xref:ModelContextProtocol.Server.McpServer>.
The C# SDK registers an instance of <xref:ModelContextProtocol.Server.McpServer> with the dependency injection container,
so tools can simply add a parameter of type <xref:ModelContextProtocol.Server.McpServer> to their method signature to access it.

#### Form mode elicitation (in-band)

For form-based elicitation, the MCP Server must specify the schema of each input value it's requesting from the user.
Primitive types (string, number, Boolean) and enum types are supported for elicitation requests.
The schema might include a description to help the user understand what's being requested.

For enum types, the SDK supports several schema formats:

- **UntitledSingleSelectEnumSchema**: A single-select enum where the enum values serve as both the value and display text.
- **TitledSingleSelectEnumSchema**: A single-select enum with separate display titles for each option (using JSON Schema `oneOf` with `const` and `title`).
- **UntitledMultiSelectEnumSchema**: A multi-select enum allowing multiple values to be selected.
- **TitledMultiSelectEnumSchema**: A multi-select enum with display titles for each option.
- **LegacyTitledEnumSchema** (deprecated): The legacy enum schema using `enumNames` for backward compatibility.

#### Default values

Each schema type supports a `Default` property that specifies a pre-populated value for the form field.
Clients should use defaults to pre-fill form fields, making it easier for users to accept common values or see expected input formats.

[!code-csharp[](Elicitation.cs?name=snippet_ElicitationDefaults)]

#### Enum schema formats

Enum schemas allow the server to present a set of choices to the user.

- <xref:ModelContextProtocol.Protocol.ElicitRequestParams.UntitledSingleSelectEnumSchema>: Simple single-select where enum values serve as both the value and display text.
- <xref:ModelContextProtocol.Protocol.ElicitRequestParams.TitledSingleSelectEnumSchema>: Single-select with separate display titles for each option using JSON Schema `oneOf` with `const` and `title`.
- <xref:ModelContextProtocol.Protocol.ElicitRequestParams.UntitledMultiSelectEnumSchema>: Multi-select allowing multiple values.
- <xref:ModelContextProtocol.Protocol.ElicitRequestParams.TitledMultiSelectEnumSchema>: Multi-select with display titles.

[!code-csharp[](Elicitation.cs?name=snippet_ElicitationEnumFormats)]

The server can request a single input or multiple inputs at once.
To help distinguish multiple inputs, each input has a unique name.

The following example demonstrates how a server could request a Boolean response from the user.

[!code-csharp[](samples/server/Tools/InteractiveTools.cs?name=snippet_GuessTheNumber)]

#### URL mode elicitation (out-of-band)

For URL mode elicitation, the server provides a URL that the user must visit to complete an action. This is useful for scenarios like OAuth flows, payment processing, or collecting sensitive credentials that should not be exposed to the MCP client.

To request a URL mode interaction, set the `Mode` to "url" and provide a `Url` and `ElicitationId` in the `ElicitRequestParams`.

[!code-csharp[](Elicitation.cs?name=snippet_ElicitationUrlMode)]

### Client support for elicitation

Clients declare their support for elicitation in their capabilities as part of the `initialize` request. Clients can support `Form` (in-band), `Url` (out-of-band), or both.

In the MCP C# SDK, this is done by configuring the capabilities and an <xref:ModelContextProtocol.Client.McpClientHandlers.ElicitationHandler> in the <xref:ModelContextProtocol.Client.McpClientOptions>:

[!code-csharp[](Elicitation.cs?name=snippet_ElicitationClientOptions)]

The `ElicitationHandler` is an asynchronous method that's called when the server requests additional information. The handler should check the `Mode` of the request:

- **Form Mode**: Present the form defined by `RequestedSchema` to the user. Return the user's input in the `Content` of the result.
- **URL Mode**: Present the `Message` and `Url` to the user. Ask for consent to open the URL. If the user consents, open the URL and return `Action="accept"`. If the user declines, return `Action="decline"`.

If the user provides the requested information (or consents to URL mode), the ElicitationHandler should return an <xref:ModelContextProtocol.Protocol.ElicitResult> with the action set to "accept".
If the user does not provide the requested information, the ElicitationHandler should return an <xref:ModelContextProtocol.Protocol.ElicitResult> with the action set to "reject" (or "decline" / "cancel").

Here's an example implementation of how a console application might handle elicitation requests:

[!code-csharp[](samples/client/Program.cs?name=snippet_ElicitationHandler)]

### Multi round-trip requests (MRTR)

[MRTR](xref:mrtr) is the SEP-2322 mechanism for server-driven input requests, finalized in protocol revision `2026-07-28`. In that revision, the server-to-client `elicitation/create` request method is removed; the recommended way to ask the user for input from a server handler is to throw <xref:ModelContextProtocol.Protocol.InputRequiredException> and let the SDK emit an <xref:ModelContextProtocol.Protocol.InputRequiredResult> on the wire.

> [!IMPORTANT]
> `ElicitAsync` throws `InvalidOperationException("Elicitation is not supported in stateless mode.")` whenever the server is running stateless — including Streamable HTTP requests served under `2026-07-28` with `Stateless = true`. Stdio servers and initialize-handshake stateful Streamable HTTP sessions continue to work via the initialize-era server-to-client `elicitation/create` request flow; an HTTP server set to `Stateless = false` refuses `2026-07-28` so dual-path clients can fall back before using that flow. For code that needs to run on stateless servers — including `2026-07-28` Streamable HTTP — throw `InputRequiredException` from your handler instead. It works under both protocols and both session modes.

For example:

[!code-csharp[](Elicitation.cs?name=snippet_ElicitationMrtr)]

> [!TIP]
> For the full protocol details, including multiple round trips, concurrent input requests, and the compatibility matrix, see [Multi Round-Trip Requests (MRTR)](xref:mrtr).

### URL elicitation required error

When a tool cannot proceed without first completing a URL-mode elicitation (for example, when third-party OAuth authorization is needed), and calling `ElicitAsync` is not practical (for example, in [stateless](xref:stateless) mode where server-to-client requests are disabled), the server might throw a <xref:ModelContextProtocol.UrlElicitationRequiredException>. This is a specialized error (JSON-RPC error code `-32042`) that signals to the client that one or more URL-mode elicitations must be completed before the original request can be retried.

#### Throwing `UrlElicitationRequiredException` on the server

A server tool can throw `UrlElicitationRequiredException` when it detects that authorization or other out-of-band interaction is required:

[!code-csharp[](Elicitation.cs?name=snippet_ElicitationUrlRequiredServer)]

The exception can include multiple elicitations if the operation requires authorization from multiple services.

#### Catching `UrlElicitationRequiredException` on the client

When the client calls a tool and receives a `UrlElicitationRequiredException`, it should:

1. Present each URL elicitation to the user (showing the URL and message).
2. Get user consent before opening each URL.
3. Optionally wait for completion notifications from the server.
4. Retry the original request after the user completes the out-of-band interactions.

[!code-csharp[](Elicitation.cs?name=snippet_ElicitationUrlRequiredClient)]

#### Listening for elicitation completion notifications

Servers can optionally send a `notifications/elicitation/complete` notification when the out-of-band interaction is complete. Clients can register a handler to receive these notifications:

[!code-csharp[](Elicitation.cs?name=snippet_ElicitationCompleteHandler)]

This pattern is particularly useful for:

- **Third-party OAuth flows**: When the MCP server needs to obtain tokens from external services on behalf of the user.
- **Payment processing**: When user confirmation is required through a secure payment interface.
- **Sensitive credential collection**: When API keys or other secrets must be entered directly on a trusted server page rather than through the MCP client.
