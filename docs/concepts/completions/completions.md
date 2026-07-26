---
title: Completions
author: jeffhandley
description: How to implement and use argument auto-completion for prompts and resources.
uid: completions
---

## Completions

MCP [completions] allow servers to provide argument auto-completion suggestions for prompt and resource template parameters. This helps clients offer a better user experience by suggesting valid values as the user types.

[completions]: https://modelcontextprotocol.io/specification/2025-11-25/server/utilities/completion

### Overview

Completions work with two types of references:

- **Prompt argument completions**: Suggest values for prompt parameters (for example, language names, style options)
- **Resource template argument completions**: Suggest values for URI template parameters (for example, file paths, resource IDs)

The server returns a <xref:ModelContextProtocol.Protocol.Completion> object containing a list of suggested values, an optional total count, and a flag indicating if more values are available.

### Implementing completions on the server

Register a completion handler when building the server. The handler receives a reference (prompt or resource template) and the current argument value:

[!code-csharp[](Completions.cs?name=snippet_CompletionHandler)]

### Automatic completions with AllowedValuesAttribute

For parameters with a known set of valid values, you can use `System.ComponentModel.DataAnnotations.AllowedValuesAttribute` on `string` parameters of prompts or resource templates. The server automatically surfaces those values as completions without needing a custom completion handler.

#### Prompt parameters

[!code-csharp[](Completions.cs?name=snippet_AllowedValuesPrompt)]

#### Resource template parameters

[!code-csharp[](Completions.cs?name=snippet_AllowedValuesResource)]

With these attributes in place, when a client sends a `completion/complete` request for the `language` or `section` argument, the server automatically filters and returns matching values based on what the user has typed so far. This approach can be combined with a custom completion handler registered via `WithCompleteHandler`; the handler's results are returned first, followed by any matching `AllowedValues`.

### Requesting completions on the client

Clients request completions using <xref:ModelContextProtocol.Client.McpClient.CompleteAsync*>. Provide a reference to the prompt or resource template, the argument name, and the current partial value.

#### Prompt argument completions

[!code-csharp[](Completions.cs?name=snippet_ClientPromptCompletion)]

#### Resource-template argument completions

[!code-csharp[](Completions.cs?name=snippet_ClientResourceCompletion)]
