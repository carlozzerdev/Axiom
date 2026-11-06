using Axiom.Harness.Application;
using System.Net;
using System.Text.Json;

namespace Axiom.Harness.Infrastructure;

public sealed class AgentToolCatalog(IEnumerable<IAgentTool> tools) : IAgentToolCatalog { public IReadOnlyList<IAgentTool> Tools { get; } = tools.ToList(); }

public sealed class FileReadTool : IAgentTool
{
    public ToolDefinition Definition { get; } = new("read_file", "Reads a UTF-8 text file located under an operator-authorized root directory.", "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"}},\"required\":[\"path\"]}");
    public async Task<ToolResult> ExecuteAsync(string argumentsJson, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var path = JsonDocument.Parse(argumentsJson).RootElement.GetProperty("path").GetString();
            if (string.IsNullOrWhiteSpace(path)) return new(false, "A file path is required.");
            var fullPath = Path.GetFullPath(path);
            var allowed = context.AllowedRoots.Any(root => fullPath.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || string.Equals(fullPath, Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase));
            if (!allowed) return new(false, "Access denied: the file is outside the authorized roots.");
            var info = new FileInfo(fullPath);
            if (!info.Exists) return new(false, "File not found.");
            if (info.Length > 1_000_000) return new(false, "File is too large; the maximum readable size is 1 MB.");
            return new(true, await File.ReadAllTextAsync(fullPath, cancellationToken));
        }
        catch (Exception ex) { return new(false, $"File read failed: {ex.Message}"); }
    }
}

public sealed class ListFilesTool : IAgentTool
{
    public ToolDefinition Definition { get; } = new("list_files", "Lists files in an operator-authorized directory. It does not read their contents.", "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"}},\"required\":[\"path\"]}");
    public Task<ToolResult> ExecuteAsync(string argumentsJson, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var path = JsonDocument.Parse(argumentsJson).RootElement.GetProperty("path").GetString();
            if (string.IsNullOrWhiteSpace(path)) return Task.FromResult(new ToolResult(false, "A directory path is required."));
            var fullPath = Path.GetFullPath(path);
            var allowed = context.AllowedRoots.Any(root => fullPath.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || string.Equals(fullPath, Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase));
            if (!allowed) return Task.FromResult(new ToolResult(false, "Access denied: the directory is outside the authorized roots."));
            if (!Directory.Exists(fullPath)) return Task.FromResult(new ToolResult(false, "Directory not found."));
            var files = Directory.EnumerateFileSystemEntries(fullPath).Take(200).Select(Path.GetFileName);
            return Task.FromResult(new ToolResult(true, string.Join(Environment.NewLine, files)));
        }
        catch (Exception ex) { return Task.FromResult(new ToolResult(false, $"Directory listing failed: {ex.Message}")); }
    }
}

public sealed class HttpGetTool(HttpClient client) : IAgentTool
{
    public ToolDefinition Definition { get; } = new("http_get", "Performs an HTTP GET request and returns a capped textual response.", "{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\",\"format\":\"uri\"}},\"required\":[\"url\"]}");
    public async Task<ToolResult> ExecuteAsync(string argumentsJson, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var url = JsonDocument.Parse(argumentsJson).RootElement.GetProperty("url").GetString();
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return new(false, "Only absolute HTTP or HTTPS URLs are allowed.");
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            var buffer = new char[1_000_000]; var read = await reader.ReadAsync(buffer, cancellationToken);
            return new(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}\n{new string(buffer, 0, read)}");
        }
        catch (Exception ex) { return new(false, $"HTTP GET failed: {ex.Message}"); }
    }
}
