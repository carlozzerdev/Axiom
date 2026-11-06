using Axiom.Harness.Application;
using Axiom.Harness.Domain;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Axiom.Harness.Infrastructure;

public sealed class OllamaOptions { public string BaseUrl { get; set; } = "http://localhost:11434"; }

public sealed class OllamaClient(HttpClient client) : IModelClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("api/tags", cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        return document.RootElement.GetProperty("models").EnumerateArray().Select(x => x.GetProperty("name").GetString()!).ToList();
    }
    public async Task<ModelResponse> ChatAsync(string model, IReadOnlyList<ModelMessage> messages, IReadOnlyList<ToolDefinition> tools, Func<string, Task>? onTextDelta, CancellationToken cancellationToken)
    {
        var body = new { model, stream = true, messages = messages.Select(ToOllamaMessage), tools = tools.Select(x => new { type = "function", function = new { name = x.Name, description = x.Description, parameters = JsonSerializer.Deserialize<JsonElement>(x.ParametersJson) } }) };
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/chat") { Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json") };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var completeText = new StringBuilder();
        var calls = new List<ToolCall>();
        await foreach (var line in ReadLinesAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var json = JsonDocument.Parse(line);
            var message = json.RootElement.GetProperty("message");
            if (message.TryGetProperty("content", out var content))
            {
                var delta = content.GetString() ?? string.Empty; completeText.Append(delta);
                if (!string.IsNullOrEmpty(delta) && onTextDelta is not null) await onTextDelta(delta);
            }
            if (message.TryGetProperty("tool_calls", out var toolCalls))
                foreach (var toolCall in toolCalls.EnumerateArray())
                {
                    var function = toolCall.GetProperty("function");
                    var name = function.GetProperty("name").GetString() ?? string.Empty;
                    var arguments = function.TryGetProperty("arguments", out var args) ? args.GetRawText() : "{}";
                    calls.Add(new ToolCall(Guid.NewGuid().ToString("N"), name, arguments));
                }
        }
        return new ModelResponse(completeText.ToString(), calls);
    }
    private static object ToOllamaMessage(ModelMessage message) => message.Role switch
    {
        AgentMessageRole.System => new { role = "system", content = message.Content },
        AgentMessageRole.User when message.ImagePath is not null => new { role = "user", content = message.Content, images = new[] { Convert.ToBase64String(File.ReadAllBytes(message.ImagePath)) } },
        AgentMessageRole.User => new { role = "user", content = message.Content },
        AgentMessageRole.Assistant => new { role = "assistant", content = message.Content, tool_calls = message.ToolCalls?.Select(x => new { function = new { name = x.Name, arguments = JsonSerializer.Deserialize<JsonElement>(x.ArgumentsJson) } }) },
        _ => new { role = "tool", content = message.Content }
    };
    private static async IAsyncEnumerable<string> ReadLinesAsync(Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream); string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null) yield return line;
    }
}
