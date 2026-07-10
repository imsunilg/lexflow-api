namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 11: "Unified client Communication tab: reverse-chron across all channels with channel icons."</summary>
public interface ICommTimelineService
{
    Task<IReadOnlyList<CommTimelineEntryDto>> GetTimelineAsync(Guid tenantId, Guid? clientId, IReadOnlyCollection<string>? channels, CancellationToken cancellationToken = default);
}

public sealed record CommTimelineEntryDto(string Channel, Guid EntityId, DateTimeOffset At, string Direction, string Summary);
