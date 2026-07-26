using System.ComponentModel;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Extensions.Apps;
using ModelContextProtocol.Server;

#pragma warning disable MCPEXP003 // MCP Apps types are experimental (see docs/list-of-diagnostics.md).

namespace Docs.Snippets.Apps;

// <snippet_AppsWeatherTools>
[McpServerToolType]
public class WeatherTools
{
    [McpServerTool, Description("Get current weather for a location")]
    [McpAppUi(ResourceUri = "ui://weather/view.html")]
    public static string GetWeather(string location) => $"Weather for {location}";

    [McpServerTool, Description("Get forecast (model-only tool)")]
    [McpAppUi(ResourceUri = "ui://weather/forecast.html", Visibility = [McpUiToolVisibility.Model])]
    public static string GetForecast(string location) => $"Forecast for {location}";
}
// </snippet_AppsWeatherTools>

internal static class AppsSetup
{
    public static void ConfigureBuilder(WebApplicationBuilder builder)
    {
        // <snippet_AppsWithMcpApps>
        builder.Services.AddMcpServer()
            .WithTools<WeatherTools>()
            .WithMcpApps();
        // </snippet_AppsWithMcpApps>
    }

    public static void ManualProcessing()
    {
        // <snippet_AppsManualProcessing>
        var tools = new[]
        {
            McpServerTool.Create(typeof(WeatherTools).GetMethod(nameof(WeatherTools.GetWeather))!),
            McpServerTool.Create(typeof(WeatherTools).GetMethod(nameof(WeatherTools.GetForecast))!),
        };

        McpApps.ApplyAppUiAttributes(tools);
        // </snippet_AppsManualProcessing>
    }

    public static void ProgrammaticApi()
    {
        // <snippet_AppsSetAppUi>
        var tool = McpServerTool.Create((string location) => $"Weather for {location}");

        McpApps.SetAppUi(tool, new McpUiToolMeta
        {
            ResourceUri = "ui://weather/view.html",
            Visibility = [McpUiToolVisibility.Model, McpUiToolVisibility.App],
        });
        // </snippet_AppsSetAppUi>
    }

    public static void Serialization(McpUiToolMeta toolMeta)
    {
        // <snippet_AppsSerialization>
        var json = JsonSerializer.Serialize(toolMeta, McpApps.SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<McpUiToolMeta>(json, McpApps.SerializerOptions);
        // </snippet_AppsSerialization>
    }
}

[McpServerToolType]
internal class AppsCapabilityTools
{
    // <snippet_AppsCheckCapability>
    [McpServerTool, Description("Get weather")]
    [McpAppUi(ResourceUri = "ui://weather/view.html")]
    public static string GetWeather(McpServer server, string location)
    {
        var uiCapability = McpApps.GetUiCapability(server.ClientCapabilities);
        if (uiCapability is not null)
        {
            // Client supports MCP Apps — the UI will be displayed
        }

        return $"Weather for {location}";
    }
    // </snippet_AppsCheckCapability>
}

[McpServerToolType]
internal class AppsAppOnlyTools
{
    // <snippet_AppsAppOnly>
    [McpServerTool, Description("Submit the weather form")]
    [McpAppUi(ResourceUri = "ui://weather/view.html", Visibility = [McpUiToolVisibility.App])]
    public static string SubmitWeatherForm(string city) => GetWeatherHtml(city);
    // </snippet_AppsAppOnly>

    private static string GetWeatherHtml(string city) => $"<html>{city}</html>";
}

[McpServerToolType]
internal class AppsDegradationTools
{
    // <snippet_AppsGracefulDegradation>
    [McpServerTool, Description("Get weather")]
    [McpAppUi(ResourceUri = "ui://weather/view.html")]
    public static string GetWeather(McpServer server, string location)
    {
        var uiCapability = McpApps.GetUiCapability(server.ClientCapabilities);
        if (uiCapability is null)
        {
            // Client doesn't support MCP Apps — return plain text
            return $"Current weather for {location}: 72°F, sunny";
        }

        // Client supports MCP Apps — the UI resource will be displayed
        return $"Weather data for {location} loaded into UI";
    }
    // </snippet_AppsGracefulDegradation>
}
