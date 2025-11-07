using MCPServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Data.Common;
using System.Net.Http.Json;
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
builder.Services.AddSingleton<CompileService>();

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

[McpServerToolType]
public static class ArcTools 
{
    [McpServerTool, Description("根据给定排数和跨度生成柱网建模动作")]
    public static string GenerateColumnGridActions(
        [Description("柱网横向排数，例如 5")] int row_nums,
        [Description("柱网纵向排数，例如 4")] int col_nums,
        [Description("柱网横向总跨度（米）")] double width1,
        [Description("柱网纵向总跨度（米）")] double width2)
    {
        var actions = new List<Dictionary<string, object>>();
        double dx = (col_nums > 1) ? width1 / (col_nums - 1) : 0;
        double dy = (row_nums > 1) ? width2 / (row_nums - 1) : 0;
        double width = 0.4;
        double depth = 0.4;
        double height = 3.6;

        for (int i = 0; i < row_nums; i++)
        {
            for (int j = 0; j < col_nums; j++)
            {
                var action_info = new Dictionary<string, object>
                {
                    ["Action"] = "CreateColumn",
                    ["Parameters"] = new Dictionary<string, double>
                    {
                        ["X"] = j * dx,
                        ["Y"] = i * dy,
                        ["Width"] = width,
                        ["Depth"] = depth,
                        ["Height"] = height
                    }
                };
                actions.Add(action_info);
            }
        }

        var result = new Dictionary<string, object>
        {
            ["Type"] = "ActionSequence",
            ["Description"] = $"{row_nums}x{col_nums} 柱网",
            ["Items"] = actions
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerToolType]
    public static class CommandTools
    {
        [McpServerTool, Description("远程编译代码并获取 DLL")]
        public static async Task<string> GenerateCmd(string sourceCode, IHttpClientFactory httpClientFactory)
        {
            var client = httpClientFactory.CreateClient();
            var payload = new { Code = sourceCode, AssemblyName = "MyGeneratedLib" };
            var response = await client.PostAsJsonAsync("http://localhost:5000/api/compile", payload);

            var json = await response.Content.ReadAsStringAsync();
            return json;
        }
    }
}

