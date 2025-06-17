using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public class CutSpeakerModel
{
    public string? Name { get; set; }
    public string? Start { get; set; }
    public string? End { get; set; }
}

public class OllamaToolRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "llama3.1:8b";

    [JsonPropertyName("messages")]
    public List<OllamaMessage> Messages { get; set; } = new();

    [JsonPropertyName("tools")]
    public List<OllamaTool> Tools { get; set; } = new();

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;
}

public class OllamaMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

public class OllamaTool
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public OllamaFunction Function { get; set; } = new();
}

public class OllamaFunction
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("parameters")]
    public JsonElement Parameters { get; set; }
}

public class OllamaResponse
{
    [JsonPropertyName("message")]
    public OllamaResponseMessage Message { get; set; } = new();
}

public class OllamaResponseMessage
{
    [JsonPropertyName("tool_calls")]
    public List<OllamaToolCall> ToolCalls { get; set; } = new();
}

public class OllamaToolCall
{
    [JsonPropertyName("function")]
    public OllamaToolCallFunction Function { get; set; } = new();
}

public class OllamaToolCallFunction
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("arguments")]
    public JsonElement Arguments { get; set; }
}

public class SpeakerExtractor
{
    private readonly HttpClient _httpClient;
    private readonly string _ollamaBaseUrl;

    public SpeakerExtractor(HttpClient httpClient, string ollamaBaseUrl = "http://localhost:11434")
    {
        _httpClient = httpClient;
        _ollamaBaseUrl = ollamaBaseUrl;
    }

    public async Task<List<CutSpeakerModel>> ExtractSpeakersAsync(string transcriptContext, string modelName = "llama3.1:8b")
    {
        var toolSchema = CreateSpeakerExtractionToolSchema();

        var request = new OllamaToolRequest
        {
            Model = modelName,
            Messages = new List<OllamaMessage>
            {
                new OllamaMessage
                {
                    Role = "system",
                    Content = "You are an expert at analyzing transcript segments to identify actual human names. " +
                              "Only extract real person names - do not include pronouns (I, you, we), contractions (I've, they're), or generic terms. " +
                              "Only return entries when you're certain they are proper human names for example, (first names like: Frank, Bob, Samuel. last names like: Smith, Johnsen, Andrews. other names like Boston, Big, Smiles) " +
                              "If no actual person names are found, return an empty array. " +
                              "Return all identified names in an array called 'names' as strictly structured JSON."
                },
                new OllamaMessage
                {
                    Role = "user",
                    Content = $"Analyze this transcript and extract only proper human names of speakers:\n\n{transcriptContext}\n\n" +
                              "Focus only on names of actual people, like 'Boston Frank', 'Bill Wilson', 'Nancy Smith' etc. " +
                              "Do not include pronouns (I, you, we) or contractions (I've, they're) as names. " +
                              "For each actual person name, identify the beginning timestamp that came before the name and the timestamp that came after the name."
                }
            },
            Tools = new List<OllamaTool>
            {
                new OllamaTool
                {
                    Type = "function",
                    Function = new OllamaFunction
                    {
                        Name = "extract_names",
                        Description = "Extract only actual human names as an array of objects in the 'names' property.",
                        Parameters = toolSchema
                    }
                }
            },
            Stream = false
        };

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_ollamaBaseUrl}/api/chat", content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        Debug.WriteLine($"Ollama responseJson: {responseJson}");

        var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(responseJson);
        var names = new List<CutSpeakerModel>();

        if (ollamaResponse?.Message?.ToolCalls?.Count > 0)
        {
            foreach (var toolCall in ollamaResponse.Message.ToolCalls)
            {
                if (toolCall.Function.Name == "extract_names")
                {
                    // Check if the arguments contain a "names" property
                    if (toolCall.Function.Arguments.TryGetProperty("names", out JsonElement namesElement))
                    {
                        // The actual response format has "names" as a string containing JSON
                        if (namesElement.ValueKind == JsonValueKind.String)
                        {
                            string namesJsonString = namesElement.GetString();

                            try
                            {
                                // Parse the string as JSON
                                using (JsonDocument namesDoc = JsonDocument.Parse(namesJsonString))
                                {
                                    JsonElement namesArray = namesDoc.RootElement;

                                    // Process the array
                                    foreach (var nameElement in namesArray.EnumerateArray())
                                    {
                                        var speaker = new CutSpeakerModel();

                                        if (nameElement.TryGetProperty("name", out var name))
                                            speaker.Name = name.GetString();
                                        else if (nameElement.TryGetProperty("Name", out var nameUpper))
                                            speaker.Name = nameUpper.GetString();

                                        if (nameElement.TryGetProperty("start", out var start))
                                            speaker.Start = start.GetString();
                                        else if (nameElement.TryGetProperty("Start", out var startUpper))
                                            speaker.Start = startUpper.GetString();

                                        if (nameElement.TryGetProperty("end", out var end))
                                            speaker.End = end.GetString();
                                        else if (nameElement.TryGetProperty("End", out var endUpper))
                                            speaker.End = endUpper.GetString();

                                        names.Add(speaker);
                                    }
                                }
                            }
                            catch (JsonException ex)
                            {
                                Debug.WriteLine($"Error parsing names JSON string: {ex.Message}");
                                Debug.WriteLine($"String content: {namesJsonString}");
                            }
                        }
                        // Fallback for if the API behavior changes and returns an array directly
                        else if (namesElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var nameElement in namesElement.EnumerateArray())
                            {
                                var speaker = new CutSpeakerModel();

                                if (nameElement.TryGetProperty("name", out var name))
                                    speaker.Name = name.GetString();
                                else if (nameElement.TryGetProperty("Name", out var nameUpper))
                                    speaker.Name = nameUpper.GetString();

                                if (nameElement.TryGetProperty("start", out var start))
                                    speaker.Start = start.GetString();
                                else if (nameElement.TryGetProperty("Start", out var startUpper))
                                    speaker.Start = startUpper.GetString();

                                if (nameElement.TryGetProperty("end", out var end))
                                    speaker.End = end.GetString();
                                else if (nameElement.TryGetProperty("End", out var endUpper))
                                    speaker.End = endUpper.GetString();

                                names.Add(speaker);
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"Unexpected names element type: {namesElement.ValueKind}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine("No 'names' property found in the function arguments");
                    }
                }
            }
        }
        else
        {
            Debug.WriteLine("No tool calls found in the response");
        }

        return names;
    }

    private JsonElement CreateSpeakerExtractionToolSchema()
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                names = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new
                            {
                                type = "string",
                                description = "Name of the speaker"
                            },
                            start = new
                            {
                                type = "string",
                                description = "Starting timestamp that came before this name segment (format: HH:MM:SS.mmm)"
                            },
                            end = new
                            {
                                type = "string",
                                description = "Ending timestamp that came after this name segment (format: HH:MM:SS.mmm)"
                            }
                        }
                    }
                }
            },
            required = new[] { "names" }
        };

        var schemaJson = JsonSerializer.Serialize(schema);
        return JsonSerializer.Deserialize<JsonElement>(schemaJson);
    }
}