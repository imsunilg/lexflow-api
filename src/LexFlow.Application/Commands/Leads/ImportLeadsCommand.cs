using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Leads;

/// <summary>POST /api/v1/leads/import (multipart). AC-L5: kicks off the Hangfire background job; see LeadImportService.</summary>
public sealed record ImportLeadsCommand(string FileName, byte[] FileContent) : IRequest<LeadImportBatchDto>;

public sealed class ImportLeadsCommandHandler(ILeadImportService leadImportService, ICurrentUserService currentUser) : IRequestHandler<ImportLeadsCommand, LeadImportBatchDto>
{
    public Task<LeadImportBatchDto> Handle(ImportLeadsCommand request, CancellationToken cancellationToken)
        => leadImportService.EnqueueImportAsync(currentUser.TenantId!.Value, currentUser.UserId, request.FileName, request.FileContent, cancellationToken);
}
