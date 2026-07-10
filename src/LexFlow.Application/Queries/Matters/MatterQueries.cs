using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Matters;

/// <summary>GET /api/v1/matters/{id}.</summary>
public sealed record GetMatterQuery(Guid MatterId) : IRequest<MatterDto>;

public sealed class GetMatterQueryHandler(IMatterService matterService, ICurrentUserService currentUser) : IRequestHandler<GetMatterQuery, MatterDto>
{
    public async Task<MatterDto> Handle(GetMatterQuery request, CancellationToken cancellationToken)
        => await matterService.GetByIdAsync(currentUser.TenantId!.Value, request.MatterId, cancellationToken)
           ?? throw new NotFoundException("Matter", request.MatterId);
}

/// <summary>GET /api/v1/matters (filters: status/type/area/lawyer/priority/q).</summary>
public sealed record GetMattersQuery(string? Status, string? MatterType, Guid? PracticeAreaId, Guid? ResponsibleLawyerId, string? Priority, string? Query) : IRequest<IReadOnlyList<MatterDto>>;

public sealed class GetMattersQueryHandler(IMatterService matterService, ICurrentUserService currentUser) : IRequestHandler<GetMattersQuery, IReadOnlyList<MatterDto>>
{
    public Task<IReadOnlyList<MatterDto>> Handle(GetMattersQuery request, CancellationToken cancellationToken)
        => matterService.GetAllAsync(currentUser.TenantId!.Value, new MatterFilter(request.Status, request.MatterType, request.PracticeAreaId, request.ResponsibleLawyerId, request.Priority, request.Query), cancellationToken);
}

public sealed record GetMatterTeamQuery(Guid MatterId) : IRequest<IReadOnlyList<MatterTeamMemberDto>>;

public sealed class GetMatterTeamQueryHandler(IMatterService matterService, ICurrentUserService currentUser) : IRequestHandler<GetMatterTeamQuery, IReadOnlyList<MatterTeamMemberDto>>
{
    public Task<IReadOnlyList<MatterTeamMemberDto>> Handle(GetMatterTeamQuery request, CancellationToken cancellationToken)
        => matterService.GetTeamAsync(currentUser.TenantId!.Value, request.MatterId, cancellationToken);
}

public sealed record GetMatterPartiesQuery(Guid MatterId) : IRequest<IReadOnlyList<MatterPartyDto>>;

public sealed class GetMatterPartiesQueryHandler(IMatterService matterService, ICurrentUserService currentUser) : IRequestHandler<GetMatterPartiesQuery, IReadOnlyList<MatterPartyDto>>
{
    public Task<IReadOnlyList<MatterPartyDto>> Handle(GetMatterPartiesQuery request, CancellationToken cancellationToken)
        => matterService.GetPartiesAsync(currentUser.TenantId!.Value, request.MatterId, cancellationToken);
}

public sealed record GetMatterImportantDatesQuery(Guid MatterId) : IRequest<IReadOnlyList<MatterImportantDateDto>>;

public sealed class GetMatterImportantDatesQueryHandler(IMatterService matterService, ICurrentUserService currentUser) : IRequestHandler<GetMatterImportantDatesQuery, IReadOnlyList<MatterImportantDateDto>>
{
    public Task<IReadOnlyList<MatterImportantDateDto>> Handle(GetMatterImportantDatesQuery request, CancellationToken cancellationToken)
        => matterService.GetImportantDatesAsync(currentUser.TenantId!.Value, request.MatterId, cancellationToken);
}

public sealed record GetMatterExpensesQuery(Guid MatterId) : IRequest<IReadOnlyList<MatterExpenseDto>>;

public sealed class GetMatterExpensesQueryHandler(IMatterService matterService, ICurrentUserService currentUser) : IRequestHandler<GetMatterExpensesQuery, IReadOnlyList<MatterExpenseDto>>
{
    public Task<IReadOnlyList<MatterExpenseDto>> Handle(GetMatterExpensesQuery request, CancellationToken cancellationToken)
        => matterService.GetExpensesAsync(currentUser.TenantId!.Value, request.MatterId, cancellationToken);
}

/// <summary>GET /api/v1/matters/{id}/timeline?types=&amp;cursor=. AC-M2.</summary>
public sealed record GetMatterTimelineQuery(Guid MatterId, string? Types, string? Cursor) : IRequest<TimelinePage>;

public sealed class GetMatterTimelineQueryHandler(IMatterService matterService, ICurrentUserService currentUser) : IRequestHandler<GetMatterTimelineQuery, TimelinePage>
{
    public Task<TimelinePage> Handle(GetMatterTimelineQuery request, CancellationToken cancellationToken)
        => matterService.GetTimelineAsync(currentUser.TenantId!.Value, request.MatterId, request.Types, request.Cursor, cancellationToken);
}

/// <summary>GET /api/v1/matters/{id}/financial-summary. AC-M4.</summary>
public sealed record GetMatterFinancialSummaryQuery(Guid MatterId) : IRequest<MatterFinancialSummaryDto>;

public sealed class GetMatterFinancialSummaryQueryHandler(IMatterService matterService, ICurrentUserService currentUser) : IRequestHandler<GetMatterFinancialSummaryQuery, MatterFinancialSummaryDto>
{
    public Task<MatterFinancialSummaryDto> Handle(GetMatterFinancialSummaryQuery request, CancellationToken cancellationToken)
        => matterService.GetFinancialSummaryAsync(currentUser.TenantId!.Value, request.MatterId, cancellationToken);
}

/// <summary>POST /api/v1/matters/conflict-check {partyNames[]}. AC-M1.</summary>
public sealed record ConflictCheckQuery(IReadOnlyList<string> PartyNames) : IRequest<IReadOnlyList<ConflictMatch>>;

public sealed class ConflictCheckQueryHandler(IConflictCheckService conflictCheckService, ICurrentUserService currentUser) : IRequestHandler<ConflictCheckQuery, IReadOnlyList<ConflictMatch>>
{
    public Task<IReadOnlyList<ConflictMatch>> Handle(ConflictCheckQuery request, CancellationToken cancellationToken)
        => conflictCheckService.CheckAsync(currentUser.TenantId!.Value, request.PartyNames, cancellationToken);
}

public sealed record GetCourtsQuery : IRequest<IReadOnlyList<CourtDto>>;

public sealed class GetCourtsQueryHandler(ILegalLookupService lookupService, ICurrentUserService currentUser) : IRequestHandler<GetCourtsQuery, IReadOnlyList<CourtDto>>
{
    public Task<IReadOnlyList<CourtDto>> Handle(GetCourtsQuery request, CancellationToken cancellationToken)
        => lookupService.GetCourtsAsync(currentUser.TenantId!.Value, cancellationToken);
}

public sealed record GetJudgesQuery(Guid? CourtId) : IRequest<IReadOnlyList<JudgeDto>>;

public sealed class GetJudgesQueryHandler(ILegalLookupService lookupService, ICurrentUserService currentUser) : IRequestHandler<GetJudgesQuery, IReadOnlyList<JudgeDto>>
{
    public Task<IReadOnlyList<JudgeDto>> Handle(GetJudgesQuery request, CancellationToken cancellationToken)
        => lookupService.GetJudgesAsync(currentUser.TenantId!.Value, request.CourtId, cancellationToken);
}

public sealed record GetPracticeAreasQuery : IRequest<IReadOnlyList<PracticeAreaDto>>;

public sealed class GetPracticeAreasQueryHandler(ILegalLookupService lookupService, ICurrentUserService currentUser) : IRequestHandler<GetPracticeAreasQuery, IReadOnlyList<PracticeAreaDto>>
{
    public Task<IReadOnlyList<PracticeAreaDto>> Handle(GetPracticeAreasQuery request, CancellationToken cancellationToken)
        => lookupService.GetPracticeAreasAsync(currentUser.TenantId!.Value, cancellationToken);
}
