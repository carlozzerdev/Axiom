namespace Axiom.Harness.Domain;

public enum AgentRunStatus { Running, Completed, Cancelled, Failed, TimedOut, StepLimitReached }
public enum AgentMessageRole { System, User, Assistant, Tool }
public enum AgentStepKind { ModelRequest, ToolCall, ToolResult, Error }

public sealed class AgentRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Model { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public AgentRunStatus Status { get; set; } = AgentRunStatus.Running;
    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public int MaxSteps { get; set; }
    public int TimeoutSeconds { get; set; }
    public List<AgentMessage> Messages { get; set; } = [];
    public List<AgentStep> Steps { get; set; } = [];
}

public sealed class AgentMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? AgentRunId { get; set; }
    public AgentMessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AgentStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? AgentRunId { get; set; }
    public int Sequence { get; set; }
    public AgentStepKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public bool Succeeded { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
