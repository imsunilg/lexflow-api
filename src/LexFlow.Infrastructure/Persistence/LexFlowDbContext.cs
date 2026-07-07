using LexFlow.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Persistence;

/// <summary>
/// Database-First DbContext: entity mappings are Fluent API configurations only
/// (see <see cref="Configurations"/>). Schema is owned exclusively by the
/// lexflow-database DbUp project — EF migrations are never generated or applied
/// from this context (no `dotnet ef migrations add` in any workflow/runbook).
/// </summary>
public sealed class LexFlowDbContext(DbContextOptions<LexFlowDbContext> options)
    : DbContext(options), IUnitOfWork
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LexFlowDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
