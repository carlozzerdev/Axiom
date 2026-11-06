using Axiom.Harness.Domain;
using System.Diagnostics;

namespace Axiom.Harness.Application;

public sealed class AgentRunner(IModelClient modelClient, IAgentToolCatalog toolCatalog, IAgentRunRepository repository) : IAgentRunner
{
    public async Task<AgentRun> RunAsync(AgentRunRequest request, IProgress<AgentProgress>? progress, CancellationToken cancellationToken)
    {
        var timeout = request.Timeout ?? TimeSpan.FromMinutes(15);
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        var token = linked.Token;
        var run = new AgentRun { Model = request.Model, Objective = request.Objective, MaxSteps = request.MaxSteps, TimeoutSeconds = (int)timeout.TotalSeconds };
        var conversation = new List<ModelMessage> { new(AgentMessageRole.System, "You are a local AI harness agent. Use tools only when useful, report concise findings, and never claim a tool result you have not received."), new(AgentMessageRole.User, request.Objective, request.ImagePath) };
        run.Messages.Add(new AgentMessage { Role = AgentMessageRole.User, Content = request.Objective, ImagePath = request.ImagePath });
        await repository.SaveAsync(run, token);
        try
        {
            for (var step = 1; step <= request.MaxSteps; step++)
            {
                var response = await modelClient.ChatAsync(request.Model, conversation, toolCatalog.Tools.Select(x => x.Definition).ToList(), async delta =>
                {
                    progress?.Report(new AgentProgress(run.Id, AgentStepKind.ModelRequest, "stream", delta));
                    await Task.CompletedTask;
                }, token);
                AddStep(run, AgentStepKind.ModelRequest, "ollama", response.Content, true, progress);
                conversation.Add(new ModelMessage(AgentMessageRole.Assistant, response.Content, ToolCalls: response.ToolCalls));
                run.Messages.Add(new AgentMessage { Role = AgentMessageRole.Assistant, Content = response.Content });
                if (response.ToolCalls.Count == 0)
                {
                    run.Status = AgentRunStatus.Completed;
                    run.FinishedAtUtc = DateTimeOffset.UtcNow;
                    await repository.SaveAsync(run, token);
                    return run;
                }
                foreach (var call in response.ToolCalls)
                {
                    var tool = toolCatalog.Tools.SingleOrDefault(x => x.Definition.Name.Equals(call.Name, StringComparison.Ordinal));
                    AddStep(run, AgentStepKind.ToolCall, call.Name, call.ArgumentsJson, tool is not null, progress);
                    var result = tool is null
                        ? new ToolResult(false, $"Tool '{call.Name}' is not available.")
                        : await tool.ExecuteAsync(call.ArgumentsJson, new ToolExecutionContext(request.AllowedRoots), token);
                    AddStep(run, AgentStepKind.ToolResult, call.Name, result.Content, result.Succeeded, progress);
                    conversation.Add(new ModelMessage(AgentMessageRole.Tool, result.Content));
                    run.Messages.Add(new AgentMessage { Role = AgentMessageRole.Tool, Content = result.Content });
                }
                await repository.SaveAsync(run, token);
            }
            run.Status = AgentRunStatus.StepLimitReached;
            run.FinishedAtUtc = DateTimeOffset.UtcNow;
            await repository.SaveAsync(run, CancellationToken.None);
            return run;
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            run.Status = AgentRunStatus.TimedOut;
            run.FinishedAtUtc = DateTimeOffset.UtcNow;
            await repository.SaveAsync(run, CancellationToken.None);
            return run;
        }
        catch (OperationCanceledException)
        {
            run.Status = AgentRunStatus.Cancelled;
            run.FinishedAtUtc = DateTimeOffset.UtcNow;
            await repository.SaveAsync(run, CancellationToken.None);
            return run;
        }
        catch (Exception ex)
        {
            AddStep(run, AgentStepKind.Error, "agent", ex.Message, false, progress);
            run.Status = AgentRunStatus.Failed;
            run.FinishedAtUtc = DateTimeOffset.UtcNow;
            await repository.SaveAsync(run, CancellationToken.None);
            return run;
        }
    }

    private static void AddStep(AgentRun run, AgentStepKind kind, string name, string payload, bool succeeded, IProgress<AgentProgress>? progress)
    {
        run.Steps.Add(new AgentStep { Sequence = run.Steps.Count + 1, Kind = kind, Name = name, Payload = payload, Succeeded = succeeded });
        progress?.Report(new AgentProgress(run.Id, kind, name, payload));
    }
}
