using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

// The sampling feature is deprecated per SEP-2577; suppress the spec-deprecation advisory so these
// illustrative snippets still compile. The pragma sits outside every snippet region and is not
// rendered in the docs.
#pragma warning disable MCP9005

namespace Docs.Snippets.Sampling;

[McpServerToolType]
public static class SamplingTools
{
    // <snippet_AsSamplingChatClient>
    [McpServerTool(Name = "SummarizeContent"), Description("Summarizes the given text")]
    public static async Task<string> Summarize(
        McpServer server,
        [Description("The text to summarize")] string text,
        CancellationToken cancellationToken)
    {
        ChatMessage[] messages =
        [
            new(ChatRole.User, "Briefly summarize the following content:"),
            new(ChatRole.User, text),
        ];

        ChatOptions options = new()
        {
            MaxOutputTokens = 256,
            Temperature = 0.3f,
        };

        return $"Summary: {await server.AsSamplingChatClient().GetResponseAsync(messages, options, cancellationToken)}";
    }
    // </snippet_AsSamplingChatClient>

    // <snippet_SamplingMrtr>
    [McpServerTool, Description("Tool that samples via MRTR")]
    public static string SampleWithMrtr(
        McpServer server,
        RequestContext<CallToolRequestParams> context)
    {
        // On retry, process the client's sampling response
        if (context.Params!.InputResponses?.TryGetValue("llm_call", out var response) is true)
        {
            var text = response.Deserialize(InputResponse.CreateMessageResultJsonTypeInfo)?.Content
                .OfType<TextContentBlock>().FirstOrDefault()?.Text;
            return $"LLM said: {text}";
        }

        if (!server.IsMrtrSupported)
        {
            return "This tool requires MRTR support (2026-07-28, or a stateful session using protocol revision 2025-11-25).";
        }

        // First call — request LLM completion from the client
        throw new InputRequiredException(
            inputRequests: new Dictionary<string, InputRequest>
            {
                ["llm_call"] = InputRequest.ForSampling(new CreateMessageRequestParams
                {
                    Messages =
                    [
                        new SamplingMessage
                        {
                            Role = Role.User,
                            Content = [new TextContentBlock { Text = "Summarize the data" }]
                        }
                    ],
                    MaxTokens = 256
                })
            },
            requestState: "awaiting-sample");
    }
    // </snippet_SamplingMrtr>
}

internal static class SamplingServer
{
    public static async Task SampleDirect(McpServer server, CancellationToken cancellationToken)
    {
        // <snippet_SampleAsync>
        CreateMessageResult result = await server.SampleAsync(
            new CreateMessageRequestParams
            {
                Messages =
                [
                    new SamplingMessage
                    {
                        Role = Role.User,
                        Content = [new TextContentBlock { Text = "What is 2 + 2?" }]
                    }
                ],
                MaxTokens = 100,
            },
            cancellationToken);

        string response = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? string.Empty;
        // </snippet_SampleAsync>
    }
}

internal static class SamplingClient
{
    public static void ConfigureCustom()
    {
#pragma warning disable CS1998 // Illustrative handler completes synchronously.
        // <snippet_CustomSamplingHandler>
        McpClientOptions options = new()
        {
            Handlers = new()
            {
                SamplingHandler = async (request, progress, cancellationToken) =>
                {
                    // Forward to your LLM, apply content filtering, etc.
                    string prompt = request?.Messages?.LastOrDefault()?.Content
                        .OfType<TextContentBlock>().FirstOrDefault()?.Text ?? string.Empty;

                    return new CreateMessageResult
                    {
                        Model = "my-model",
                        Role = Role.Assistant,
                        Content = [new TextContentBlock { Text = $"Response to: {prompt}" }]
                    };
                }
            }
        };
        // </snippet_CustomSamplingHandler>
#pragma warning restore CS1998
    }
}
