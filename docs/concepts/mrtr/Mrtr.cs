using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

#pragma warning disable MCP9005 // Docs reference the deprecated Sampling handler seam (SEP-2577).

namespace Docs.Snippets.Mrtr;

internal static class MrtrClient
{
    public static void Configure()
    {
        // <snippet_MrtrClientOptions>
        // Client — the SDK prefers 2026-07-28 (and therefore MRTR) by default.
        var clientOptions = new McpClientOptions
        {
            Handlers = new McpClientHandlers
            {
                ElicitationHandler = HandleElicitationAsync,
                SamplingHandler = HandleSamplingAsync,
            }
        };
        // </snippet_MrtrClientOptions>
    }

    private static ValueTask<ElicitResult> HandleElicitationAsync(ElicitRequestParams? request, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    private static ValueTask<CreateMessageResult> HandleSamplingAsync(CreateMessageRequestParams? request, IProgress<ProgressNotificationValue> progress, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}

[McpServerToolType]
internal static class MrtrTools
{
    // <snippet_MrtrAnswerTool>
    [McpServerTool, Description("Tool managing its own MRTR flow")]
    public static string AnswerTool(
        McpServer server,
        RequestContext<CallToolRequestParams> context,
        [Description("The user's question")] string question)
    {
        var requestState = context.Params!.RequestState;
        var inputResponses = context.Params!.InputResponses;

        // On retry, process the client's responses
        if (requestState is not null && inputResponses is not null)
        {
            var elicitResult = inputResponses["user_answer"].Deserialize(InputResponse.ElicitResultJsonTypeInfo);
            return $"You answered: {elicitResult?.Content?.FirstOrDefault().Value}";
        }

        if (!server.IsMrtrSupported)
        {
            return "MRTR is not supported by this client.";
        }

        // First call — request user input
        throw new InputRequiredException(
            inputRequests: new Dictionary<string, InputRequest>
            {
                ["user_answer"] = InputRequest.ForElicitation(new ElicitRequestParams
                {
                    Message = $"Please answer: {question}",
                    RequestedSchema = new()
                    {
                        Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                        {
                            ["answer"] = new ElicitRequestParams.StringSchema
                            {
                                Description = "Your answer"
                            }
                        }
                    }
                })
            },
            requestState: "awaiting-answer");
    }
    // </snippet_MrtrAnswerTool>

    // <snippet_MrtrDeferred>
    [McpServerTool, Description("Tool that defers work using requestState")]
    public static string DeferredTool(
        McpServer server,
        RequestContext<CallToolRequestParams> context)
    {
        var requestState = context.Params!.RequestState;

        if (requestState is not null)
        {
            // Resume deferred work
            var state = JsonSerializer.Deserialize<MyState>(
                Convert.FromBase64String(requestState));
            return $"Completed step {state!.Step}";
        }

        if (!server.IsMrtrSupported)
        {
            return "MRTR is not supported by this client.";
        }

        // Defer work to a later retry
        var initialState = new MyState { Step = 1 };
        throw new InputRequiredException(
            requestState: Convert.ToBase64String(
                JsonSerializer.SerializeToUtf8Bytes(initialState)));
    }
    // </snippet_MrtrDeferred>

    // <snippet_MrtrWizard>
    [McpServerTool, Description("Multi-step wizard")]
    public static string WizardTool(
        McpServer server,
        RequestContext<CallToolRequestParams> context)
    {
        var requestState = context.Params!.RequestState;
        var inputResponses = context.Params!.InputResponses;

        if (requestState == "step-2" && inputResponses is not null)
        {
            var name = inputResponses["name"].Deserialize(InputResponse.ElicitResultJsonTypeInfo)?.Content?.FirstOrDefault().Value;
            var age = inputResponses["age"].Deserialize(InputResponse.ElicitResultJsonTypeInfo)?.Content?.FirstOrDefault().Value;
            return $"Welcome, {name}! You are {age} years old.";
        }

        if (requestState == "step-1" && inputResponses is not null)
        {
            var name = inputResponses["name"].Deserialize(InputResponse.ElicitResultJsonTypeInfo)?.Content?.FirstOrDefault().Value;

            // Second round — ask for age
            throw new InputRequiredException(
                inputRequests: new Dictionary<string, InputRequest>
                {
                    ["age"] = InputRequest.ForElicitation(new ElicitRequestParams
                    {
                        Message = $"Hi {name}! How old are you?",
                        RequestedSchema = new()
                        {
                            Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                            {
                                ["age"] = new ElicitRequestParams.NumberSchema
                                {
                                    Description = "Your age"
                                }
                            }
                        }
                    })
                },
                requestState: "step-2");
        }

        if (!server.IsMrtrSupported)
        {
            return "MRTR is not supported. Please use a compatible client.";
        }

        // First round — ask for name
        throw new InputRequiredException(
            inputRequests: new Dictionary<string, InputRequest>
            {
                ["name"] = InputRequest.ForElicitation(new ElicitRequestParams
                {
                    Message = "What's your name?",
                    RequestedSchema = new()
                    {
                        Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                        {
                            ["name"] = new ElicitRequestParams.StringSchema
                            {
                                Description = "Your name"
                            }
                        }
                    }
                })
            },
            requestState: "step-1");
    }
    // </snippet_MrtrWizard>

    // <snippet_MrtrConfirmTool>
    [McpServerTool, Description("Deletes a file (with required confirmation).")]
    public static string DeleteFile(
        McpServer server,
        RequestContext<CallToolRequestParams> context,
        [Description("The path of the file to delete")] string path,
        [Description("User confirmation to delete the file")] bool confirm = false)
    {
        // Handles four client scenarios:
        //  1. Explicit opt-in: client sends `confirm: true`
        //  2. MRTR round-trip request: client sends `InputResponses["confirm"]` with action `accept`
        //  3. MRTR initial request: server elicits input (with automatic SDK down-level bridge)
        //  4. Session-less down-level: server returns a guidance message requesting explicit opt-in

        // (1) Explicit opt-in. Works on any client, including down-level session-less
        //     because a previous response gave instructions for passing `confirm: true`.
        //     These requests are typically sent after (4) returns a guidance message.
        var deletionConfirmed = confirm;

        // (2) MRTR round-trip request. Works with native MRTR support or the automatic down-level
        //     SDK bridge after (3) throws an `InputRequiredException` to elicit input.
        if (!confirm && context.Params?.InputResponses?.TryGetValue("confirm", out var confirmResponse) is true)
        {
            var confirmResult = confirmResponse.Deserialize(InputResponse.ElicitResultJsonTypeInfo);
            deletionConfirmed = confirmResult?.IsAccepted is true;

            if (!deletionConfirmed) return "Deletion cancelled";
        }

        // (1) or (2) Explicit opt-in or confirmation input received; proceed with the deletion
        if (deletionConfirmed)
            return $"Deleted {path}.";

        // (3) MRTR initial request: elicit input to confirm the deletion. This uses the
        //     2026-07-28 MRTR input request, but the SDK provides an automatic bridge to
        //     a legacy elicitation on a down-level, stateful session. When the bridge can
        //     be provided, `server.IsMrtrSupported` is `true` and the exception leads to
        //     a legacy elicitation response automatically.
        if (server.IsMrtrSupported)
        {
            throw new InputRequiredException(
                inputRequests: new Dictionary<string, InputRequest>
                {
                    ["confirm"] = InputRequest.ForElicitation(new ElicitRequestParams
                    {
                        Message = $"Delete {path}? This cannot be undone.",
                        RequestedSchema = new(),
                    })
                },
                requestState: path);   // opaque; echoed back to us on the retry
        }

        // (4) Down-level and stateless: we can't prompt an elicitation through an MRTR
        //     round-trip request or an elicitation. Return a natural language response
        //     with guidance for sending an explicit opt-in.
        return $"Deletion requires user confirmation. Confirm by resending with `confirm: true`.";
    }
    // </snippet_MrtrConfirmTool>

    private sealed class MyState
    {
        public int Step { get; set; }
    }
}

internal static class MrtrGuidance
{
    public static string Describe(McpServer server)
    {
        // <snippet_MrtrGuidance>
        if (!server.IsMrtrSupported)
        {
            return "This tool needs interactive input. Connect with a client that negotiates MCP "
                 + "protocol revision 2026-07-28, or use a stateful session using revision 2025-11-25 "
                 + "so the server can resolve the input requests for you.";
        }
        // </snippet_MrtrGuidance>

        return string.Empty;
    }
}
