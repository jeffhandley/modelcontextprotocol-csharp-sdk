using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

// The illustrative server tools below return synchronously (they throw or short-circuit
// before awaiting), and the ternary in the MRTR sample dereferences a value it just
// null-checked with ?. — both are intentional in these doc fragments.
#pragma warning disable CS1998 // async method lacks await
#pragma warning disable CS8602 // dereference of a possibly null reference

namespace Docs.Snippets.Elicitation;

internal static class ElicitationSnippets
{
    public static async Task Defaults(McpServer server, CancellationToken cancellationToken)
    {
        // <snippet_ElicitationDefaults>
        var result = await server.ElicitAsync(new ElicitRequestParams
        {
            Message = "Configure your preferences",
            RequestedSchema = new ElicitRequestParams.RequestSchema
            {
                Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                {
                    ["name"] = new ElicitRequestParams.StringSchema
                    {
                        Description = "Your display name",
                        Default = "User"
                    },
                    ["maxResults"] = new ElicitRequestParams.NumberSchema
                    {
                        Description = "Maximum number of results",
                        Default = 25
                    },
                    ["enableNotifications"] = new ElicitRequestParams.BooleanSchema
                    {
                        Description = "Enable push notifications",
                        Default = true
                    },
                    ["theme"] = new ElicitRequestParams.UntitledSingleSelectEnumSchema
                    {
                        Description = "UI theme",
                        Enum = ["light", "dark", "system"],
                        Default = "system"
                    }
                }
            }
        }, cancellationToken);
        // </snippet_ElicitationDefaults>
    }

    public static void EnumFormats()
    {
        var properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
        {
            // <snippet_ElicitationEnumFormats>
            // Titled single-select: display titles differ from values
            ["priority"] = new ElicitRequestParams.TitledSingleSelectEnumSchema
            {
                Description = "Task priority",
                OneOf =
                [
                    new() { Const = "p0", Title = "Critical (P0)" },
                    new() { Const = "p1", Title = "High (P1)" },
                    new() { Const = "p2", Title = "Normal (P2)" },
                ],
                Default = "p2"
            },

            // Multi-select: user can select multiple values
            ["tags"] = new ElicitRequestParams.UntitledMultiSelectEnumSchema
            {
                Description = "Tags to apply",
                Items = new()
                {
                    Enum = ["bug", "feature", "docs", "test"]
                },
                Default = ["bug"]
            }
            // </snippet_ElicitationEnumFormats>
        };
    }

    public static async Task UrlMode(McpServer server, CancellationToken cancellationToken)
    {
        // <snippet_ElicitationUrlMode>
        var elicitationId = Guid.NewGuid().ToString();
        var result = await server.ElicitAsync(
            new ElicitRequestParams
            {
                Mode = "url",
                ElicitationId = elicitationId,
                Url = $"https://auth.example.com/oauth/authorize?state={elicitationId}",
                Message = "Please authorize access to your account by logging in through your browser."
            },
            cancellationToken);
        // </snippet_ElicitationUrlMode>
    }

    public static void ClientOptions()
    {
        // <snippet_ElicitationClientOptions>
        var options = new McpClientOptions
        {
            Capabilities = new ClientCapabilities
            {
                Elicitation = new ElicitationCapability
                {
                    Form = new FormElicitationCapability(),
                    Url = new UrlElicitationCapability()
                }
            },
            Handlers = new McpClientHandlers
            {
                ElicitationHandler = HandleElicitationAsync
            }
        };
        // </snippet_ElicitationClientOptions>
    }

    static ValueTask<ElicitResult> HandleElicitationAsync(ElicitRequestParams? request, CancellationToken cancellationToken)
        => ValueTask.FromResult(new ElicitResult());

    public static async Task UrlRequiredClient(McpClient client)
    {
        // <snippet_ElicitationUrlRequiredClient>
        try
        {
            var result = await client.CallToolAsync("AccessThirdPartyResource");
            Console.WriteLine($"Tool succeeded: {result.Content[0]}");
        }
        catch (UrlElicitationRequiredException ex)
        {
            Console.WriteLine($"Authorization required: {ex.Message}");

            // Process each required elicitation
            foreach (var elicitation in ex.Elicitations)
            {
                Console.WriteLine($"\nServer requests URL interaction:");
                Console.WriteLine($"  Message: {elicitation.Message}");
                Console.WriteLine($"  URL: {elicitation.Url}");
                Console.WriteLine($"  Elicitation ID: {elicitation.ElicitationId}");

                // Show security warning and get user consent
                Console.Write("\nDo you want to open this URL? (y/n): ");
                var consent = Console.ReadLine();

                if (consent?.ToLower() == "y")
                {
                    // Open the URL in the system browser
                    Process.Start(new ProcessStartInfo(elicitation.Url!) { UseShellExecute = true });

                    Console.WriteLine("Waiting for you to complete the interaction in your browser...");
                    // Optionally listen for notifications/elicitation/complete notification
                }
            }

            // After user completes the out-of-band interaction, retry the tool call
            Console.Write("\nPress Enter to retry the tool call...");
            Console.ReadLine();

            var retryResult = await client.CallToolAsync("AccessThirdPartyResource");
            Console.WriteLine($"Tool succeeded on retry: {retryResult.Content[0]}");
        }
        // </snippet_ElicitationUrlRequiredClient>
    }

    public static async Task CompletionHandler(McpClient client)
    {
        // <snippet_ElicitationCompleteHandler>
        await using var completionHandler = client.RegisterNotificationHandler(
            NotificationMethods.ElicitationCompleteNotification,
            async (notification, cancellationToken) =>
            {
                var payload = notification.Params?.Deserialize<ElicitationCompleteNotificationParams>(
                    McpJsonUtilities.DefaultOptions);

                if (payload is not null)
                {
                    Console.WriteLine($"Elicitation {payload.ElicitationId} completed!");
                    // Signal that the client can now retry the original request
                }
            });
        // </snippet_ElicitationCompleteHandler>
    }
}

[McpServerToolType]
internal sealed class ElicitationTools
{
    // <snippet_ElicitationMrtr>
    [McpServerTool, Description("Tool that elicits via MRTR")]
    public static string ElicitWithMrtr(
        McpServer server,
        RequestContext<CallToolRequestParams> context)
    {
        // On retry, process the client's elicitation response
        if (context.Params!.InputResponses?.TryGetValue("user_input", out var response) is true)
        {
            var elicitResult = response.Deserialize(InputResponse.ElicitResultJsonTypeInfo);
            return elicitResult?.Action == "accept"
                ? $"User accepted: {elicitResult.Content?.FirstOrDefault().Value}"
                : "User declined.";
        }

        if (!server.IsMrtrSupported)
        {
            return "This tool requires MRTR support (2026-07-28, or a stateful session using protocol revision 2025-11-25).";
        }

        // First call — request user input
        throw new InputRequiredException(
            inputRequests: new Dictionary<string, InputRequest>
            {
                ["user_input"] = InputRequest.ForElicitation(new ElicitRequestParams
                {
                    Message = "Please confirm the action",
                    RequestedSchema = new()
                    {
                        Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                        {
                            ["confirm"] = new ElicitRequestParams.BooleanSchema
                            {
                                Description = "Confirm the action"
                            }
                        }
                    }
                })
            },
            requestState: "awaiting-confirmation");
    }
    // </snippet_ElicitationMrtr>

    // <snippet_ElicitationUrlRequiredServer>
    [McpServerTool, Description("A tool that requires third-party authorization")]
    public async Task<string> AccessThirdPartyResource(McpServer server, CancellationToken token)
    {
        // Check if we already have valid credentials for this user
        // (In a real app, you'd check stored tokens based on user identity)
        bool hasValidCredentials = false;

        if (!hasValidCredentials)
        {
            // Generate a unique elicitation ID for tracking
            var elicitationId = Guid.NewGuid().ToString();

            // Throw the exception to signal the client needs to complete URL elicitation
            throw new UrlElicitationRequiredException(
                "Authorization is required to access the third-party service.",
                [
                    new ElicitRequestParams
                    {
                        Mode = "url",
                        ElicitationId = elicitationId,
                        Url = $"https://auth.example.com/connect?elicitationId={elicitationId}",
                        Message = "Please authorize access to your Example Co account."
                    }
                ]);
        }

        // Proceed with the authorized operation
        return "Successfully accessed the resource!";
    }
    // </snippet_ElicitationUrlRequiredServer>
}
