using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Kb;

/// <summary>AC-KB1/AC-KB2/§Module 12 User Flow #4.</summary>
public sealed record KbSearchAppQuery(string Q, string? Type, Guid? CourtId, int? YearFrom, string? Tag) : IRequest<KbSearchResult>;

public sealed class KbSearchAppQueryHandler(IKbSearchService service, ICurrentUserService currentUser) : IRequestHandler<KbSearchAppQuery, KbSearchResult>
{
    public Task<KbSearchResult> Handle(KbSearchAppQuery request, CancellationToken cancellationToken)
        => service.SearchAsync(currentUser.TenantId!.Value, request.Q, request.Type, request.CourtId, request.YearFrom, request.Tag, cancellationToken);
}
