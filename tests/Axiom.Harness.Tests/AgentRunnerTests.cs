using Axiom.Harness.Application;
using Axiom.Harness.Domain;
using Xunit;

namespace Axiom.Harness.Tests;

public sealed class AgentRunnerTests
{
    [Fact]
    public async Task Completes_when_model_returns_a_final_response()
    {
        var runner = new AgentRunner(new FakeModel(new("done", [])), new FakeCatalog(), new MemoryRepository());
        var run = await runner.RunAsync(new("test", "Say done", null, []), null, CancellationToken.None);
        Assert.Equal(AgentRunStatus.Completed, run.Status); Assert.Contains(run.Messages, x => x.Content == "done");
    }
    [Fact]
    public async Task Stops_at_step_limit_when_model_only_requests_tools()
    {
        var runner = new AgentRunner(new FakeModel(new("", [new("1", "echo", "{}") ])), new FakeCatalog(), new MemoryRepository());
        var run = await runner.RunAsync(new("test", "loop", null, [], 2), null, CancellationToken.None);
        Assert.Equal(AgentRunStatus.StepLimitReached, run.Status);
    }
    private sealed class FakeModel(ModelResponse response) : IModelClient
    {
        public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<ModelResponse> ChatAsync(string model, IReadOnlyList<ModelMessage> messages, IReadOnlyList<ToolDefinition> tools, Func<string, Task>? delta, CancellationToken ct) => Task.FromResult(response);
    }
    private sealed class FakeCatalog : IAgentToolCatalog
    {
        public IReadOnlyList<IAgentTool> Tools { get; } = [new FakeTool()];
        private sealed class FakeTool : IAgentTool { public ToolDefinition Definition { get; } = new("echo", "", "{}"); public Task<ToolResult> ExecuteAsync(string a, ToolExecutionContext c, CancellationToken ct) => Task.FromResult(new ToolResult(true, "ok")); }
    }
    private sealed class MemoryRepository : IAgentRunRepository
    {
        public Task SaveAsync(AgentRun run, CancellationToken ct) => Task.CompletedTask;
        public Task<AgentRun?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult<AgentRun?>(null);
        public Task<IReadOnlyList<AgentRun>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AgentRun>>([]);
    }
}
