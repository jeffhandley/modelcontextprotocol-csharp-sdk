using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace ModelContextProtocol.Server;

/// <summary>
/// Intercepts a server-initiated outgoing request (sampling, elicitation, or roots) so that it can be
/// redirected through an alternate channel instead of being sent directly to the client.
/// </summary>
/// <param name="method">The request method identifier (e.g., <c>"sampling/createMessage"</c>).</param>
/// <param name="params">The serialized request parameters.</param>
/// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
/// <returns>The serialized result node for the request.</returns>
[Experimental(Experimentals.Subclassing_DiagnosticId, UrlFormat = Experimentals.Subclassing_Url)]
public delegate ValueTask<JsonNode?> McpOutgoingRequestInterceptor(string method, JsonNode? @params, CancellationToken cancellationToken);

/// <summary>
/// Represents an instance of a Model Context Protocol (MCP) server that connects to and communicates with an MCP client.
/// </summary>
public abstract partial class McpServer : McpSession
{
#pragma warning disable MCPEXP002
    private static readonly AsyncLocal<McpOutgoingRequestInterceptor?> s_currentOutgoingRequestInterceptor = new();
#pragma warning restore MCPEXP002

    /// <summary>
    /// Gets or sets the ambient interceptor that redirects server-initiated outgoing requests
    /// (sampling, elicitation, roots) for the current asynchronous flow.
    /// </summary>
    /// <remarks>
    /// This is an SDK extensibility hook intended for bolt-on packages (such as the MCP Tasks extension)
    /// that run tool logic in the background and need to surface server-to-client requests through an
    /// alternate channel. When set, <see cref="ElicitAsync(ModelContextProtocol.Protocol.ElicitRequestParams, CancellationToken)"/>,
    /// <see cref="SampleAsync(ModelContextProtocol.Protocol.CreateMessageRequestParams, CancellationToken)"/>, and
    /// <see cref="RequestRootsAsync(ModelContextProtocol.Protocol.ListRootsRequestParams, CancellationToken)"/> route
    /// through the interceptor instead of sending directly to the client.
    /// </remarks>
    [Experimental(Experimentals.Subclassing_DiagnosticId, UrlFormat = Experimentals.Subclassing_Url)]
    public static McpOutgoingRequestInterceptor? CurrentOutgoingRequestInterceptor
    {
        get => s_currentOutgoingRequestInterceptor.Value;
        set => s_currentOutgoingRequestInterceptor.Value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="McpServer"/> class.
    /// </summary>
    [Experimental(Experimentals.Subclassing_DiagnosticId, UrlFormat = Experimentals.Subclassing_Url)]
    protected McpServer()
    {
    }

    /// <summary>
    /// Gets the capabilities supported by the client.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These capabilities are established during the initialization handshake and indicate
    /// which features the client supports, such as sampling, roots, and other
    /// protocol-specific functionality.
    /// </para>
    /// <para>
    /// Server implementations can check these capabilities to determine which features
    /// are available when interacting with the client.
    /// </para>
    /// </remarks>
    public abstract ClientCapabilities? ClientCapabilities { get; }

    /// <summary>
    /// Gets the version and implementation information of the connected client.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property contains identification information about the client that has connected to this server,
    /// including its name and version. This information is provided by the client during initialization.
    /// </para>
    /// <para>
    /// Server implementations can use this information for logging, tracking client versions, 
    /// or implementing client-specific behaviors.
    /// </para>
    /// </remarks>
    public abstract Implementation? ClientInfo { get; }

    /// <summary>
    /// Gets the options used to construct this server.
    /// </summary>
    /// <remarks>
    /// These options define the server's capabilities, protocol version, and other configuration
    /// settings that were used to initialize the server.
    /// </remarks>
    public abstract McpServerOptions ServerOptions { get; }

    /// <summary>
    /// Gets the service provider for the server.
    /// </summary>
    public abstract IServiceProvider? Services { get; }

    /// <summary>Gets the last logging level set by the client, or <see langword="null"/> if it's never been set.</summary>
    [Obsolete(Obsoletions.DeprecatedLogging_Message, DiagnosticId = Obsoletions.Deprecated_DiagnosticId, UrlFormat = Obsoletions.Deprecated_Url)]
    public abstract LoggingLevel? LoggingLevel { get; }

    /// <summary>
    /// Gets a value indicating whether the connected client supports Multi Round-Trip Requests (MRTR).
    /// </summary>
    /// <remarks>
    /// <para>
    /// When this property returns <see langword="true"/>, tool handlers can throw
    /// <see cref="Protocol.InputRequiredException"/> to return an <see cref="Protocol.InputRequiredResult"/>
    /// with <see cref="Protocol.InputRequiredResult.InputRequests"/> and/or
    /// <see cref="Protocol.InputRequiredResult.RequestState"/> to the client.
    /// </para>
    /// <para>
    /// When this property returns <see langword="false"/>, tool handlers should provide a fallback
    /// experience (for example, returning a text message explaining that the client does not support
    /// the required feature) instead of throwing <see cref="Protocol.InputRequiredException"/>.
    /// </para>
    /// </remarks>
    public virtual bool IsMrtrSupported => false;

    /// <summary>
    /// Runs the server, listening for and handling client requests.
    /// </summary>
    public abstract Task RunAsync(CancellationToken cancellationToken = default);
}
