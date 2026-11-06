using Axiom.Harness.Application;
using Axiom.Harness.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Axiom.Harness.Infrastructure;

public sealed class HarnessDbContext(DbContextOptions<HarnessDbContext> options) : DbContext(options)
{
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();
    public DbSet<AgentMessage> AgentMessages => Set<AgentMessage>();
    public DbSet<AgentStep> AgentSteps => Set<AgentStep>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<AgentRun>(entity =>
        {
            entity.ToTable("AgentRuns"); entity.HasKey(x => x.Id); entity.Property(x => x.Model).HasMaxLength(200); entity.Property(x => x.Objective).HasColumnType("nvarchar(max)");
            entity.HasMany(x => x.Messages).WithOne().HasForeignKey(x => x.AgentRunId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Steps).WithOne().HasForeignKey(x => x.AgentRunId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<AgentMessage>(entity => { entity.ToTable("AgentMessages"); entity.HasKey(x => x.Id); entity.Property(x => x.Content).HasColumnType("nvarchar(max)"); entity.Property(x => x.ImagePath).HasMaxLength(2048); });
        builder.Entity<AgentStep>(entity => { entity.ToTable("AgentSteps"); entity.HasKey(x => x.Id); entity.Property(x => x.Name).HasMaxLength(200); entity.Property(x => x.Payload).HasColumnType("nvarchar(max)"); });
    }
}

/// <summary>Enables <c>dotnet ef</c> without starting the WinForms application.</summary>
public sealed class HarnessDbContextFactory : IDesignTimeDbContextFactory<HarnessDbContext>
{
    public HarnessDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<HarnessDbContext>();
        builder.UseSqlServer("Server=localhost;Database=AxiomHarness;Trusted_Connection=True;TrustServerCertificate=True");
        return new HarnessDbContext(builder.Options);
    }
}

public sealed class EfAgentRunRepository(IDbContextFactory<HarnessDbContext> factory) : IAgentRunRepository
{
    public async Task SaveAsync(AgentRun run, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var existing = await db.AgentRuns.Include(x => x.Messages).Include(x => x.Steps).SingleOrDefaultAsync(x => x.Id == run.Id, cancellationToken);
        if (existing is null)
        {
            foreach (var message in run.Messages) message.AgentRunId = run.Id;
            foreach (var step in run.Steps) step.AgentRunId = run.Id;
            db.AgentRuns.Add(run);
        }
        else
        {
            existing.Model = run.Model; existing.Objective = run.Objective; existing.Status = run.Status;
            existing.FinishedAtUtc = run.FinishedAtUtc; existing.MaxSteps = run.MaxSteps; existing.TimeoutSeconds = run.TimeoutSeconds;
            var knownMessages = existing.Messages.Select(x => x.Id).ToHashSet();
            var newMessages = run.Messages.Where(x => !knownMessages.Contains(x.Id)).ToList();
            foreach (var message in newMessages) message.AgentRunId = run.Id;
            db.AgentMessages.AddRange(newMessages);
            var knownSteps = existing.Steps.Select(x => x.Id).ToHashSet();
            var newSteps = run.Steps.Where(x => !knownSteps.Contains(x.Id)).ToList();
            foreach (var step in newSteps) step.AgentRunId = run.Id;
            db.AgentSteps.AddRange(newSteps);
        }
        await db.SaveChangesAsync(cancellationToken);
    }
    public async Task<AgentRun?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.AgentRuns.Include(x => x.Messages).Include(x => x.Steps).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
    public async Task<IReadOnlyList<AgentRun>> ListAsync(CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.AgentRuns.Include(x => x.Messages).Include(x => x.Steps).OrderByDescending(x => x.StartedAtUtc).ToListAsync(cancellationToken);
    }
}
