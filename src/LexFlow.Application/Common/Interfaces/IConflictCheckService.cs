namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// AC-M1 / FR-013: Elasticsearch fuzzy match of candidate party names against
/// legal.matter_parties + crm.clients (opposing party on one matter is a client/party
/// on another → conflict of interest). Behind an interface so it's mockable in tests —
/// no test in this codebase should hit a real Elasticsearch cluster.
/// </summary>
public interface IConflictCheckService
{
    Task<IReadOnlyList<ConflictMatch>> CheckAsync(Guid tenantId, IReadOnlyList<string> partyNames, CancellationToken cancellationToken = default);

    /// <summary>Indexes (or re-indexes) one party name so future conflict checks can find it. Called whenever a matter/case party is created.</summary>
    Task IndexPartyAsync(Guid tenantId, string name, string sourceType, Guid sourceId, Guid? matterId, string? matterNumber, CancellationToken cancellationToken = default);
}

public sealed record ConflictMatch(string MatchedName, double Score, string SourceType, Guid SourceId, Guid? MatterId, string? MatterNumber);
