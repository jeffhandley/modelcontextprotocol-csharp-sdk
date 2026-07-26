using System.ComponentModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Docs.Snippets.Prompts;

// <snippet_PromptsSimple>
[McpServerPromptType]
public class MyPrompts
{
    [McpServerPrompt, Description("A simple greeting prompt")]
    public static ChatMessage Greeting()
        => new(ChatRole.User, "Hello! How can you help me today?");
}
// </snippet_PromptsSimple>

// <snippet_PromptsWithArgs>
[McpServerPromptType]
public class CodePrompts
{
    [McpServerPrompt, Description("Generates a code review prompt")]
    public static IEnumerable<ChatMessage> CodeReview(
        [Description("The programming language")] string language,
        [Description("The code to review")] string code) =>
        [
            new(ChatRole.User, $"Please review the following {language} code:\n\n```{language}\n{code}\n```"),
            new(ChatRole.Assistant, "I'll review the code for correctness, style, and potential improvements.")
        ];
}
// </snippet_PromptsWithArgs>

internal static class PromptRegistration
{
    public static void Register(WebApplicationBuilder builder)
    {
        // <snippet_PromptsRegister>
        builder.Services.AddMcpServer()
            .WithHttpTransport(o => o.Stateless = true)
            .WithPrompts<MyPrompts>()
            .WithPrompts<CodePrompts>();
        // </snippet_PromptsRegister>
    }
}

[McpServerPromptType]
internal class RichPrompts
{
    // <snippet_PromptsImage>
    [McpServerPrompt, Description("A prompt that includes an image for analysis")]
    public static IEnumerable<ChatMessage> AnalyzeImage(
        [Description("Instructions for the analysis")] string instructions)
    {
        byte[] imageBytes = LoadSampleImage();
        return
        [
            new ChatMessage(ChatRole.User,
            [
                new TextContent($"Please analyze this image: {instructions}"),
                new DataContent(imageBytes, "image/png")
            ])
        ];
    }
    // </snippet_PromptsImage>

    // <snippet_PromptsEmbedded>
    [McpServerPrompt, Description("A prompt that includes a document resource")]
    public static IEnumerable<PromptMessage> ReviewDocument(
        [Description("The document ID to review")] string documentId)
    {
        string content = LoadDocument(documentId); // application logic to load by ID
        return
        [
            new PromptMessage
            {
                Role = Role.User,
                Content = new TextContentBlock { Text = "Please review the following document:" }
            },
            new PromptMessage
            {
                Role = Role.User,
                Content = new EmbeddedResourceBlock
                {
                    Resource = new TextResourceContents
                    {
                        Uri = $"docs://documents/{documentId}",
                        MimeType = "text/plain",
                        Text = content
                    }
                }
            }
        ];
    }
    // </snippet_PromptsEmbedded>

    private static byte[] LoadSampleImage() => [1, 2, 3];
    private static string LoadDocument(string documentId) => $"contents of {documentId}";
}

internal static class BlobPromptExample
{
    public static PromptMessage BlobExample(byte[] pdfBytes) =>
        // <snippet_PromptsBlob>
        new PromptMessage
        {
            Role = Role.User,
            Content = new EmbeddedResourceBlock
            {
                Resource = BlobResourceContents.FromBytes(pdfBytes, "data://report.pdf", "application/pdf")
            }
        }
        // </snippet_PromptsBlob>
        ;
}

internal static class PromptConsumers
{
    public static async Task ListPrompts(McpClient client)
    {
        // <snippet_PromptsList>
        IList<McpClientPrompt> prompts = await client.ListPromptsAsync();

        foreach (var prompt in prompts)
        {
            Console.WriteLine($"{prompt.Name}: {prompt.Description}");

            // Show available arguments
            if (prompt.ProtocolPrompt.Arguments is { Count: > 0 })
            {
                foreach (var arg in prompt.ProtocolPrompt.Arguments)
                {
                    var required = arg.Required == true ? " (required)" : "";
                    Console.WriteLine($"  - {arg.Name}: {arg.Description}{required}");
                }
            }
        }
        // </snippet_PromptsList>
    }

    public static async Task GetPrompt(McpClient client)
    {
        // <snippet_PromptsGet>
        GetPromptResult result = await client.GetPromptAsync(
            "code_review",
            new Dictionary<string, object?>
            {
                ["language"] = "csharp",
                ["code"] = "public static int Add(int a, int b) => a + b;"
            });

        // Process the returned messages (PromptMessage has a single Content block)
        foreach (var message in result.Messages)
        {
            Console.WriteLine($"[{message.Role}]:");
            switch (message.Content)
            {
                case TextContentBlock text:
                    Console.WriteLine($"  {text.Text}");
                    break;
                case ImageContentBlock image:
                    Console.WriteLine($"  [image] {image.MimeType}");
                    break;
                case EmbeddedResourceBlock resource:
                    Console.WriteLine($"  Resource: {resource.Resource.Uri}");
                    break;
            }
        }
        // </snippet_PromptsGet>
    }

    public static async Task SendNotify(McpServer server)
    {
        // <snippet_PromptsNotify>
        // After adding or removing prompts dynamically
        await server.SendNotificationAsync(
            NotificationMethods.PromptListChangedNotification,
            new PromptListChangedNotificationParams());
        // </snippet_PromptsNotify>
    }

    public static void HandleNotify(McpClient mcpClient)
    {
        // <snippet_PromptsHandle>
        mcpClient.RegisterNotificationHandler(
            NotificationMethods.PromptListChangedNotification,
            async (notification, cancellationToken) =>
            {
                var updatedPrompts = await mcpClient.ListPromptsAsync(cancellationToken: cancellationToken);
                Console.WriteLine($"Prompt list updated. {updatedPrompts.Count} prompts available.");
            });
        // </snippet_PromptsHandle>
    }
}
