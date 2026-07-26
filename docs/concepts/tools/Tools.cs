// Compiled samples for docs/concepts/tools/tools.md.
//
// Each // <snippet_X> ... </snippet_X> region is transcluded into the documentation via
// [!code-csharp[](../../samples/Tools.cs?name=snippet_X)]. Code outside the regions
// (helper stubs, wrapping methods, and using directives) provides the compilable context
// and is intentionally not rendered in the documentation.

using System.ComponentModel;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Docs.Snippets.Tools;

// <snippet_MyTools>
[McpServerToolType]
public class MyTools
{
    [McpServerTool, Description("Echoes the input message back")]
    public static string Echo([Description("The message to echo")] string message)
        => $"Echo: {message}";
}
// </snippet_MyTools>

internal static class ToolRegistration
{
    public static void Register(WebApplicationBuilder builder)
    {
        // <snippet_RegisterTools>
        builder.Services.AddMcpServer()
            .WithHttpTransport(o => o.Stateless = true)
            .WithTools<MyTools>();
        // </snippet_RegisterTools>
    }
}

[McpServerToolType]
public class ContentTools
{
    // <snippet_Greet>
    [McpServerTool, Description("Returns a greeting")]
    public static string Greet(string name) => $"Hello, {name}!";
    // </snippet_Greet>

    // <snippet_GenerateImage>
    [McpServerTool, Description("Returns a generated image")]
    public static ImageContentBlock GenerateImage()
    {
        byte[] pngBytes = CreateImage(); // your image generation logic
        return ImageContentBlock.FromBytes(pngBytes, "image/png");
    }
    // </snippet_GenerateImage>

    // <snippet_Synthesize>
    [McpServerTool, Description("Returns a synthesized audio clip")]
    public static AudioContentBlock Synthesize(string text)
    {
        byte[] wavBytes = TextToSpeech(text); // your audio synthesis logic
        return AudioContentBlock.FromBytes(wavBytes, "audio/wav");
    }
    // </snippet_Synthesize>

    // <snippet_GetDocument>
    [McpServerTool, Description("Returns a document as an embedded resource")]
    public static EmbeddedResourceBlock GetDocument()
    {
        return new EmbeddedResourceBlock
        {
            Resource = new TextResourceContents
            {
                Uri = "docs://readme",
                MimeType = "text/plain",
                Text = "This is the document content."
            }
        };
    }
    // </snippet_GetDocument>

    // <snippet_GetBinaryData>
    [McpServerTool, Description("Returns a binary resource")]
    public static EmbeddedResourceBlock GetBinaryData(string id)
    {
        byte[] data = LoadData(id); // application logic to load data by ID
        return new EmbeddedResourceBlock
        {
            Resource = BlobResourceContents.FromBytes(data, $"data://items/{id}", "application/octet-stream")
        };
    }
    // </snippet_GetBinaryData>

    // <snippet_DescribeImage>
    [McpServerTool, Description("Returns text and an image")]
    public static IEnumerable<ContentBlock> DescribeImage()
    {
        byte[] imageBytes = GetImage();
        return
        [
            new TextContentBlock { Text = "Here is the generated image:" },
            ImageContentBlock.FromBytes(imageBytes, "image/png"),
            new TextContentBlock { Text = "The image shows a landscape." }
        ];
    }
    // </snippet_DescribeImage>

    // <snippet_Divide>
    [McpServerTool, Description("Divides two numbers")]
    public static double Divide(double a, double b)
    {
        if (b == 0)
        {
            // ArgumentException is not an McpException, so the client receives a generic message:
            // "An error occurred invoking 'divide'."
            throw new ArgumentException("Cannot divide by zero");
        }

        return a / b;
    }
    // </snippet_Divide>

    // <snippet_Process>
    [McpServerTool, Description("Processes the input")]
    public static string Process(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            // Propagates as a JSON-RPC error with code -32602 (InvalidParams)
            // and message "Missing required input"
            throw new McpProtocolException("Missing required input", McpErrorCode.InvalidParams);
        }

        return $"Processed: {input}";
    }
    // </snippet_Process>

    // <snippet_Search>
    [McpServerTool, Description("Searches for items")]
    public static string Search(
        [Description("The search query string")] string query,
        [Description("Maximum results to return (1-100)")] int maxResults = 10)
    {
        // Schema will include descriptions and default value for maxResults
        return $"Found matches for '{query}' (max {maxResults}).";
    }
    // </snippet_Search>

    // <snippet_ExecuteSql>
    [McpServerTool, Description("Executes a SQL query in a specific region")]
    public static string ExecuteSql(
        [McpHeader("Region"), Description("Target datacenter region")] string region,
        [Description("The SQL query to execute")] string query)
    {
        // Clients will send an additional HTTP header:
        //   Mcp-Param-Region: <region value>
        return $"Executed '{query}' in {region}.";
    }
    // </snippet_ExecuteSql>

    private static byte[] CreateImage() => Array.Empty<byte>();
    private static byte[] TextToSpeech(string text) => Array.Empty<byte>();
    private static byte[] LoadData(string id) => Array.Empty<byte>();
    private static byte[] GetImage() => Array.Empty<byte>();
}

internal static class ContentAnnotationExample
{
    public static ContentBlock Create()
    {
        ContentBlock annotated =
            // <snippet_Annotations>
            new TextContentBlock
            {
                Text = "Detailed debug information",
                Annotations = new Annotations
                {
                    Audience = [Role.Assistant], // Only for the LLM, not the user
                    Priority = 0.3f             // Low priority (0.0 to 1.0)
                }
            }
            // </snippet_Annotations>
            ;
        return annotated;
    }
}

internal static class ToolClientUsage
{
    public static async Task ConsumeTools(McpClient client)
    {
        // <snippet_ConsumeTools>
        // List available tools
        IList<McpClientTool> tools = await client.ListToolsAsync();

        foreach (var tool in tools)
        {
            Console.WriteLine($"{tool.Name}: {tool.Description}");
        }

        // Call a tool by finding it in the list
        McpClientTool echoTool = tools.First(t => t.Name == "echo");
        CallToolResult result = await echoTool.CallAsync(
            new Dictionary<string, object?> { ["message"] = "Hello!" });

        // Process the result content blocks
        foreach (var content in result.Content)
        {
            switch (content)
            {
                case TextContentBlock text:
                    Console.WriteLine(text.Text);
                    break;
                case ImageContentBlock image:
                    File.WriteAllBytes("output.png", image.DecodedData.ToArray());
                    break;
                case AudioContentBlock audio:
                    File.WriteAllBytes("output.wav", audio.DecodedData.ToArray());
                    break;
                case EmbeddedResourceBlock resource:
                    if (resource.Resource is TextResourceContents textResource)
                        Console.WriteLine(textResource.Text);
                    break;
            }
        }
        // </snippet_ConsumeTools>
    }

    public static async Task CheckErrors(McpClient client)
    {
        // <snippet_CheckErrors>
        CallToolResult result = await client.CallToolAsync("divide", new Dictionary<string, object?>
        {
            ["a"] = 10,
            ["b"] = 0
        });

        if (result.IsError is true)
        {
            // Prints: "Tool error: An error occurred invoking 'divide'."
            Console.WriteLine($"Tool error: {result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text}");
        }
        // </snippet_CheckErrors>
    }

    public static void HandleToolListChanged(McpClient mcpClient)
    {
        // <snippet_HandleToolListChanged>
        mcpClient.RegisterNotificationHandler(
            NotificationMethods.ToolListChangedNotification,
            async (notification, cancellationToken) =>
            {
                // Refresh the tool list
                var updatedTools = await mcpClient.ListToolsAsync(cancellationToken: cancellationToken);
                Console.WriteLine($"Tool list updated. {updatedTools.Count} tools available.");
            });
        // </snippet_HandleToolListChanged>
    }

    public static async Task PreloadKnownTools(McpClient client)
    {
        // <snippet_PreloadKnownTools>
        // Build the tool definition with x-mcp-header annotations
        var tool = new Tool
        {
            Name = "execute_sql",
            InputSchema = JsonDocument.Parse("""
                {
                    "type": "object",
                    "properties": {
                        "region": {
                            "type": "string",
                            "x-mcp-header": "Region"
                        },
                        "query": {
                            "type": "string"
                        }
                    }
                }
                """).RootElement.Clone(),
        };

        // Pre-load the tool definition — no ListToolsAsync needed
        client.AddKnownTools([tool]);

        // This call now sends an Mcp-Param-Region header automatically
        var result = await client.CallToolAsync("execute_sql",
            new Dictionary<string, object?> { ["region"] = "us-west-2", ["query"] = "SELECT 1" });
        // </snippet_PreloadKnownTools>
        _ = result;
    }

    public static void RemoveKnownTools(McpClient client)
    {
        // <snippet_RemoveKnownTools>
        // Remove specific known tools by name
        client.RemoveKnownTools(["execute_sql"]);

        // Or remove all known tools at once
        client.ClearKnownTools();
        // </snippet_RemoveKnownTools>
    }
}

internal static class ToolServerNotifications
{
    public static async Task SendToolListChanged(McpServer server)
    {
        // <snippet_SendToolListChanged>
        // After adding or removing tools dynamically
        await server.SendNotificationAsync(
            NotificationMethods.ToolListChangedNotification,
            new ToolListChangedNotificationParams());
        // </snippet_SendToolListChanged>
    }
}
