using MCPServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Scriban;
using System.ComponentModel;
using System.Data.Common;
using System.Dynamic;
using System.Net.Http.Json;
using System.Text.Json;

// 等待调试
#if DEBUG
Console.WriteLine("等待调试器 attach...");
while (!System.Diagnostics.Debugger.IsAttached)
{
    Thread.Sleep(100);
}
#endif


// 这是本地通信
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

builder.Services.AddHttpClient();

await builder.Build().RunAsync();

[McpServerToolType]
public static class EchoTool
{
    [McpServerTool, Description("Echoes the message back to the client.")]
    public static string Echo(string message) => $"Hello from C#: {message}";

    [McpServerTool, Description("Echoes in reverse the message sent by the client.")]
    public static string ReverseEcho(string message) => new string(message.Reverse().ToArray());
}

[McpServerToolType]
public static class CmdGenataionTools
{
    //[McpServerTool, Description("将传入建模动作的JSON格式描述编译为可执行的dll文件，结果为JSON格式文件，包含成功信息和文件地址")]
    public static async Task<string> CompileCode(
        [Description("传入建模动作的JSON格式文件")] string actions_json,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        var completeCode = new List<string>();

        var _executingCode = new List<string>();
        var doc = JsonDocument.Parse(actions_json);
        var root = doc.RootElement;

        var items = root.GetProperty("Items");
        var _description = root.GetProperty("Description").GetString() ?? "";
        var _cmd_name = root.GetProperty("CmdName").GetString() ?? "";

        foreach (var item in items.EnumerateArray())
        {
            string actionName = item.GetProperty("FunctionName").GetString() ?? "";
            var parameters = item.GetProperty("Parameters");

            var paramList = new List<string>();
            foreach (var p in parameters.EnumerateObject()) // 历一个 JSON 对象的 所有 key-value 对
            {
                paramList.Add($"{p.Name}: {p.Value}");
            }
            string argumentString = string.Join(", ", paramList);
            string finalCall = $"McpActions.{actionName}({argumentString});";
            _executingCode.Add(finalCall);
        }

        var executing_texts = string.Join("\n", _executingCode);

        // Scriban 的控制语句（statement）必须处于一行的最开头（不能被空格缩进），否则解析不到
        string templateText = @"
using DBARC;
using ArchDe_EB;
using DBArcFunc;
using DBSTR;
using EB3D;
using EB3DDB;
using EB3DFunction;
using EBCore;
using EBDemo;
using EBPlugIn;
using FamilyEditor;
using GeoAlgo;
using GuiDB;
using GuiFunction;
using GuiMath;
using GuiStructural;
using System;

public class {{ cmd_name }} : IExternalCmd
{
    public string Describe() { return ""{{ description }}"";}
    public string Name() { return ""{{ description }}"";}

    public Result Execute(EBDB EbDb)
    {
        try{
            // 确认当前视图为2D视图
            string msg = """";
            var CurView = EbDb.ActivView2D?.GView;
            if (CurView == null)
            {
                msg = ""没找到2D视图"";
                throw new Exception(msg);
            }

            // 执行逻辑
            using (new Transaction(""{{ description }}""))
            {
                {{executing_texts}}
            }

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            EBMessageBox.Show(""插件运行错误:"" + ex.Message);
            return Result.Failed;
        }
    }
}
";

        var data = new
        {
            description = _description,
            executing_texts,
            cmd_name = _cmd_name
        };
        var template = Template.Parse(templateText);
        string resultCode = template.Render(data);

        var client = httpClientFactory.CreateClient();
        var payload = new { Code = resultCode, AssemblyName = "AiGenCmd" };
        var response = await client.PostAsJsonAsync("https://localhost:44355/api/compile", payload);
        var json = await response.Content.ReadAsStringAsync();
        return json;
    }

    [McpServerTool, Description("根据柱网的排数列数、总跨度和总进深生成矩形柱网")]
    public static async Task<string> GenerateColumnGridActions(
        [Description("柱网横向排数，例如 5")] int row_nums,
        [Description("柱网纵向排数，例如 4")] int col_nums,
        [Description("柱网横向总跨度（毫米）")] double width1,
        [Description("柱网纵向总跨度（毫米）")] double width2,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        var actions = new List<Dictionary<string, object>>();
        double dx = (col_nums > 1) ? width1 / (col_nums - 1) : 0;
        double dy = (row_nums > 1) ? width2 / (row_nums - 1) : 0;
        double width = 500;
        double depth = 500;
        double height = 3000;

        for (int i = 0; i < row_nums; i++)
        {
            for (int j = 0; j < col_nums; j++)
            {
                var action_info = new Dictionary<string, object>
                {
                    ["FunctionName"] = "CreateColumn",
                    ["Parameters"] = new Dictionary<string, object>
                    {
                        ["X"] = (j * dx),
                        ["Y"] = (i * dy),
                        ["Width"] = width,
                        ["Depth"] = depth,
                        ["Height"] = height,
                        ["CurView"] = "CurView"
                    }
                };
                actions.Add(action_info);
            }
        }

        var result = new Dictionary<string, object>
        {
            ["CmdName"] = "CreateColumns_AI",
            ["Description"] = $"{row_nums}x{col_nums} 柱网",
            ["Items"] = actions
        };

        var result_json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        return await CompileCode(result_json, httpClientFactory,cancellationToken);
    }
}

