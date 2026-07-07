using LexFlow.Domain.Common;

namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Aggregate-agnostic repository contract. Kept free of EF Core/Npgsql types so
/// Application has zero persistence dependencies; LexFlow.Infrastructure supplies
/// the EF Core-backed implementation (Database-First against the lexflow-database schema).
/// </summary>
public interface IRepository<T> where T : Entity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Remove(T entity);
}
