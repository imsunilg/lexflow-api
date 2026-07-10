using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Leads;

/// <summary>GET /api/v1/leads/{id}.</summary>
public sealed record GetLeadQuery(Guid LeadId) : IRequest<LeadDto>;

public sealed class GetLeadQueryHandler(ILeadService leadService, ICurrentUserService currentUser) : IRequestHandler<GetLeadQuery, LeadDto>
{
    public async Task<LeadDto> Handle(GetLeadQuery request, CancellationToken cancellationToken)
        => await leadService.GetByIdAsync(currentUser.TenantId!.Value, request.LeadId, cancellationToken)
           ?? throw new NotFoundException("Lead", request.LeadId);
}

/// <summary>GET /api/v1/leads (filter: stage,source,owner,score,created range,q).</summary>
public sealed record GetLeadsQuery(string? Stage, Guid? SourceId, Guid? OwnerId, int? MinScore, DateTimeOffset? CreatedFrom, DateTimeOffset? CreatedTo, string? Query, string? Status)
    : IRequest<IReadOnlyList<LeadDto>>;

public sealed class GetLeadsQueryHandler(ILeadService leadService, ICurrentUserService currentUser) : IRequestHandler<GetLeadsQuery, IReadOnlyList<LeadDto>>
{
    public Task<IReadOnlyList<LeadDto>> Handle(GetLeadsQuery request, CancellationToken cancellationToken)
        => leadService.GetAllAsync(
            currentUser.TenantId!.Value,
            new LeadFilter(request.Stage, request.SourceId, request.OwnerId, request.MinScore, request.CreatedFrom, request.CreatedTo, request.Query, request.Status),
            cancellationToken);
}

/// <summary>POST /api/v1/leads/check-duplicates {name,email,phone}. Module 2 User Flow step 2 (pg_trgm fuzzy match).</summary>
public sealed record CheckLeadDuplicatesQuery(string? Name, string? Email, string? PhoneE164) : IRequest<IReadOnlyList<DuplicateMatchDto>>;

public sealed class CheckLeadDuplicatesQueryHandler(ILeadService leadService, ICurrentUserService currentUser) : IRequestHandler<CheckLeadDuplicatesQuery, IReadOnlyList<DuplicateMatchDto>>
{
    public Task<IReadOnlyList<DuplicateMatchDto>> Handle(CheckLeadDuplicatesQuery request, CancellationToken cancellationToken)
        => leadService.CheckDuplicatesAsync(currentUser.TenantId!.Value, request.Name, request.Email, request.PhoneE164, cancellationToken);
}

/// <summary>GET /api/v1/leads/export?format=csv|xlsx.</summary>
public sealed record ExportLeadsQuery(string? Stage, Guid? SourceId, Guid? OwnerId, string? Format) : IRequest<byte[]>;

public sealed class ExportLeadsQueryHandler(ILeadService leadService, ICurrentUserService currentUser) : IRequestHandler<ExportLeadsQuery, byte[]>
{
    public Task<byte[]> Handle(ExportLeadsQuery request, CancellationToken cancellationToken)
        => leadService.ExportAsync(
            currentUser.TenantId!.Value,
            new LeadFilter(request.Stage, request.SourceId, request.OwnerId, null, null, null, null, null),
            request.Format ?? "csv",
            cancellationToken);
}

/// <summary>GET /api/v1/leads/import/{batchId}.</summary>
public sealed record GetLeadImportBatchQuery(Guid BatchId) : IRequest<LeadImportBatchDto>;

public sealed class GetLeadImportBatchQueryHandler(ILeadImportService leadImportService, ICurrentUserService currentUser) : IRequestHandler<GetLeadImportBatchQuery, LeadImportBatchDto>
{
    public async Task<LeadImportBatchDto> Handle(GetLeadImportBatchQuery request, CancellationToken cancellationToken)
        => await leadImportService.GetBatchAsync(currentUser.TenantId!.Value, request.BatchId, cancellationToken)
           ?? throw new NotFoundException("LeadImportBatch", request.BatchId);
}
