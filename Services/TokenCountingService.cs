using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace os.Services
{
    public interface ITokenCountingService
    {
        Task<int> CountTokensAsync(string text);
    }

    public class OllamaTokenCountingService : ITokenCountingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;
        private readonly string _model;

        public OllamaTokenCountingService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiUrl = configuration["Ollama:ApiUrl"].Replace("/chat", "/tokenize");
            _model = configuration["Ollama:Model"];
        }

        public async Task<int> CountTokensAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            var request = new TokenizeRequest
            {
                Model = _model,
                Content = text
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(_apiUrl, content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<TokenizeResponse>(responseJson);

                return result?.Tokens?.Length ?? 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error counting tokens: {ex.Message}");
                return -1; // Indicates error
            }
        }

        private class TokenizeRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; }

            [JsonPropertyName("content")]
            public string Content { get; set; }
        }

        private class TokenizeResponse
        {
            [JsonPropertyName("tokens")]
            public int[] Tokens { get; set; }
        }
    }
}