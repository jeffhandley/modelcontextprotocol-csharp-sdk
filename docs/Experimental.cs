using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol;
using ModelContextProtocol.AspNetCore;

namespace Docs.Snippets.Experimental;

internal static class PerCallSuppression
{
    public static void Configure(HttpServerTransportOptions options)
    {
        // <snippet_PerCallSuppression>
        #pragma warning disable MCPEXP002 // RunSessionHandler is experimental and may change.
        options.RunSessionHandler = static (_, _, _) => Task.CompletedTask;
        #pragma warning restore MCPEXP002
        // </snippet_PerCallSuppression>
    }
}

internal static class CustomContextConfig
{
    public static JsonSerializerOptions Build()
    {
        // <snippet_TypeInfoResolverChain>
        JsonSerializerOptions options = new()
        {
            TypeInfoResolverChain =
            {
                McpJsonUtilities.DefaultOptions.TypeInfoResolver!,
                MyCustomContext.Default,
            }
        };
        // </snippet_TypeInfoResolverChain>
        return options;
    }
}

[JsonSerializable(typeof(string))]
internal sealed partial class MyCustomContext : JsonSerializerContext;
