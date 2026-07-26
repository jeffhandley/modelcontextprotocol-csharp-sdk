using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Docs.Snippets.Completions.CompletionHelpers;

namespace Docs.Snippets.Completions;

internal static class ServerCompletions
{
    public static void Configure(WebApplicationBuilder builder)
    {
#pragma warning disable CS1998 // Illustrative handler completes synchronously.
        // <snippet_CompletionHandler>
        builder.Services.AddMcpServer()
            .WithHttpTransport(o => o.Stateless = true)
            .WithPrompts<MyPrompts>()
            .WithResources<MyResources>()
            .WithCompleteHandler(async (ctx, ct) =>
            {
                if (ctx.Params is not { } @params)
                    throw new McpProtocolException("Params are required.", McpErrorCode.InvalidParams);

                var argument = @params.Argument;

                // Handle prompt argument completions
                if (@params.Ref is PromptReference promptRef)
                {
                    var suggestions = argument.Name switch
                    {
                        "language" => new[] { "csharp", "python", "javascript", "typescript", "go", "rust" },
                        "style" => new[] { "casual", "formal", "technical", "friendly" },
                        _ => Array.Empty<string>()
                    };

                    // Filter suggestions based on what the user has typed so far
                    var filtered = suggestions.Where(s => s.StartsWith(argument.Value, StringComparison.OrdinalIgnoreCase)).ToList();

                    return new CompleteResult
                    {
                        Completion = new Completion
                        {
                            Values = filtered,
                            Total = filtered.Count,
                            HasMore = false
                        }
                    };
                }

                // Handle resource template argument completions
                if (@params.Ref is ResourceTemplateReference resourceRef)
                {
                    var availableIds = new[] { "1", "2", "3", "4", "5" };
                    var filtered = availableIds.Where(id => id.StartsWith(argument.Value)).ToList();

                    return new CompleteResult
                    {
                        Completion = new Completion
                        {
                            Values = filtered,
                            Total = filtered.Count,
                            HasMore = false
                        }
                    };
                }

                return new CompleteResult();
            });
        // </snippet_CompletionHandler>
#pragma warning restore CS1998
    }
}

// <snippet_AllowedValuesPrompt>
[McpServerPromptType]
public class MyPrompts
{
    [McpServerPrompt, Description("Generates a code review prompt")]
    public static ChatMessage CodeReview(
        [Description("The programming language")]
        [AllowedValues("csharp", "python", "javascript", "typescript", "go", "rust")]
        string language,
        [Description("The code to review")] string code)
        => new(ChatRole.User, $"Please review the following {language} code:\n\n```{language}\n{code}\n```");
}
// </snippet_AllowedValuesPrompt>

// <snippet_AllowedValuesResource>
[McpServerResourceType]
public class MyResources
{
    [McpServerResource(UriTemplate = "config://settings/{section}"), Description("Reads a configuration section")]
    public static string ReadConfig(
        [AllowedValues("general", "network", "security", "logging")]
        string section)
        => GetConfig(section);
}
// </snippet_AllowedValuesResource>

internal static class ClientCompletions
{
    public static async Task PromptCompletion(McpClient client)
    {
        // <snippet_ClientPromptCompletion>
        // Get completions for a prompt argument
        CompleteResult result = await client.CompleteAsync(
            new PromptReference { Name = "code_review" },
            argumentName: "language",
            argumentValue: "type");

        // result.Completion.Values might contain: ["typescript"]
        foreach (var suggestion in result.Completion.Values)
        {
            Console.WriteLine($"  {suggestion}");
        }

        if (result.Completion.HasMore == true)
        {
            Console.WriteLine($"  ... and more ({result.Completion.Total} total)");
        }
        // </snippet_ClientPromptCompletion>
    }

    public static async Task ResourceCompletion(McpClient client)
    {
        // <snippet_ClientResourceCompletion>
        // Get completions for a resource template argument
        CompleteResult result = await client.CompleteAsync(
            new ResourceTemplateReference { Uri = "file:///{path}" },
            argumentName: "path",
            argumentValue: "src/");

        foreach (var suggestion in result.Completion.Values)
        {
            Console.WriteLine($"  {suggestion}");
        }
        // </snippet_ClientResourceCompletion>
    }
}

internal static class CompletionHelpers
{
    public static string GetConfig(string section) => "";
}
