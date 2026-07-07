namespace LexFlow.Application.Common.Interfaces;

/// <summary>Commits repository changes in a single transaction. Implemented by the EF Core DbContext in Infrastructure.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
