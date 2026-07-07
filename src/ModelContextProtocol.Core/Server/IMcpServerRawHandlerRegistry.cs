using ModelContextProtocol.Protocol;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

namespace ModelContextProtocol.Server;

/// <summary>
/// Represents a low-level request handler that operates directly on the JSON-RPC request and produces
/// the serialized result node, bypassing the strongly-typed handler pipeline.
/// </summary>
/// <param name="request">The incoming JSON-RPC request.</param>
/// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
/// <returns>The serialized result node, or <see langword="null"/>.</returns>
[Experimental(Experimentals.Subclassing_DiagnosticId, UrlFormat = Experimentals.Subclassing_Url)]
public delegate ValueTask<JsonNode?> McpRawRequestHandler(JsonRpcRequest request, CancellationToken cancellationToken);

/// <summary>
/// Provides a low-level seam for registering and wrapping raw request handlers on an <see cref="McpServer"/>.
/// </summary>
/// <remarks>
/// This is an SDK extensibility hook intended for bolt-on packages (such as the MCP Tasks extension) that
/// need to register new request methods or wrap existing handlers without taking a compile-time dependency
/// on the feature in the core SDK. Configure it via <see cref="McpServerOptions.RawRequestHandlerConfigurators"/>.
/// </remarks>
[Experimental(Experimentals.Subclassing_DiagnosticId, UrlFormat = Experimentals.Subclassing_Url)]
public interface IMcpServerRawHandlerRegistry
{
    /// <summary>
    /// Gets the <see cref="McpServer"/> these handlers are being configured for.
    /// </summary>
    McpServer Server { get; }

    /// <summary>
    /// Determines whether a handler is currently registered for the specified method.
    /// </summary>
    /// <param name="method">The request method identifier (e.g., <c>"tools/call"</c>).</param>
    /// <returns><see langword="true"/> if a handler is registered; otherwise, <see langword="false"/>.</returns>
    bool ContainsHandler(string method);

    /// <summary>
    /// Registers (or replaces) the raw handler for the specified method.
    /// </summary>
    /// <param name="method">The request method identifier (e.g., <c>"tasks/get"</c>).</param>
    /// <param name="handler">The raw handler to register.</param>
    void SetHandler(string method, McpRawRequestHandler handler);

    /// <summary>
    /// Wraps the existing raw handler for the specified method, if one is registered.
    /// </summary>
    /// <param name="method">The request method identifier (e.g., <c>"tools/call"</c>).</param>
    /// <param name="wrap">A function that receives the existing inner handler and returns a wrapping handler.</param>
    /// <returns><see langword="true"/> if a handler was present and wrapped; otherwise, <see langword="false"/>.</returns>
    bool TryWrapHandler(string method, Func<McpRawRequestHandler, McpRawRequestHandler> wrap);

    /// <summary>
    /// Determines whether the specified request was negotiated under the draft protocol revision.
    /// </summary>
    /// <param name="request">The JSON-RPC request to inspect.</param>
    /// <returns><see langword="true"/> if the request was negotiated under the draft revision; otherwise, <see langword="false"/>.</returns>
    bool IsDraftProtocolRequest(JsonRpcRequest request);
}
