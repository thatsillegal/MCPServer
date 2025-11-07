using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MCPServer
{
    public class CompileService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CompileService> _logger;

        public CompileService(HttpClient httpClient, ILogger<CompileService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<string> SendToCompileServerAsync(string json)
        {
            try
            {
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("http://your-compile-server/api/compile", content);

                response.EnsureSuccessStatusCode();

                // 后端返回编译结果，比如返回下载链接或 base64 文件内容
                string result = await response.Content.ReadAsStringAsync();
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send compile request");
                return $"Error: {ex.Message}";
            }
        }
    }
}
