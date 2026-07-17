namespace ModelContextProtocol.Legacy.Tasks;

internal static class LegacyTasksApiObsoletion
{
    public const string DiagnosticId = "MCP9007";
    public const string Message = "The 2025-11-30 Tasks APIs are retained only for source migration from MCP C# SDK 1.x. Use ModelContextProtocol.Extensions.Tasks for new code.";
    public const string Url = "https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/list-of-diagnostics.md#obsolete-apis";
}
