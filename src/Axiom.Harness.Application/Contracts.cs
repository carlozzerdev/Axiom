using Axiom.Harness.Domain;

namespace Axiom.Harness.Application;

public sealed record ToolDefinition(string Name, string Description, string ParametersJson);
public sealed record ToolCall(string Id, string Name, string ArgumentsJson);
public sealed record ModelMessage(AgentMessageRole Role, string Content, string? ImagePath = null, IReadOnlyList<ToolCall>? ToolCalls = null);
public sealed record ModelResponse(string Content, IReadOnlyList<ToolCall> ToolCalls);
public sealed record AgentRunRequest(string Model, string Objective, string? ImagePath, IReadOnlyList<string> AllowedRoots, int MaxSteps = 25, TimeSpan? Timeout = null);
public sealed record AgentProgress(Guid RunId, AgentStepKind Kind, string Name, string Content);
public sealed record ToolExecutionContext(IReadOnlyList<string> AllowedRoots);
public sealed record ToolResult(bool Succeeded, string Content);

public interface IModelClient
{
    Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken);
    Task<ModelResponse> ChatAsync(string model, IReadOnlyList<ModelMessage> messages, IReadOnlyList<ToolDefinition> tools, Func<string, Task>? onTextDelta, CancellationToken cancellationToken);
}
public interface IAgentTool
{
    ToolDefinition Definition { get; }
    Task<ToolResult> ExecuteAsync(string argumentsJson, ToolExecutionContext context, CancellationToken cancellationToken);
}
public interface IAgentToolCatalog { IReadOnlyList<IAgentTool> Tools { get; } }
public interface IAgentRunRepository
{
    Task SaveAsync(AgentRun run, CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentRun>> ListAsync(CancellationToken cancellationToken);
    Task<AgentRun?> GetAsync(Guid id, CancellationToken cancellationToken);
}
public interface IAgentRunner
{
    Task<AgentRun> RunAsync(AgentRunRequest request, IProgress<AgentProgress>? progress, CancellationToken cancellationToken);
}
