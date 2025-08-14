using MCPServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<MonkeyService>();


await builder.Build().RunAsync();

/// <summary>
/// In our startup code, the WithToolsFromAssembly will scan the assembly for classes with the McpServerToolType attribute and
/// register all methods with the McpServerTool attribute. Notice that the McpServerTool has a Description which will be fed into
/// any client connecting to the server. This description helps the client determine which tool to call.
/// </summary>
[McpServerToolType]
public static class EchoTool
{
    [McpServerTool, Description("Echoes the message back to the client.")]
    public static string Echo(string message) => $"Hello from C#: {message}";

    [McpServerTool, Description("Echoes in reverse the message sent by the client.")]
    public static string ReverseEcho(string message) => new string(message.Reverse().ToArray());
}

[McpServerToolType]
public static class MonkeyTools
{
    [McpServerTool, Description("Get a list of monkeys.")]
    public static async Task<string> GetMonkeys(MonkeyService monkeyService)
    {
        var monkeys = await monkeyService.GetMonkeys();
        return JsonSerializer.Serialize(monkeys);
    }

    [McpServerTool, Description("Get a monkey by name.")]
    public static async Task<string> GetMonkey(MonkeyService monkeyService, [Description("The name of the monkey to get details for")] string name)
    {
        var monkey = await monkeyService.GetMonkey(name);
        return JsonSerializer.Serialize(monkey);
    }
}