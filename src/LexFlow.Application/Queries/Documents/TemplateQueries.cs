using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Documents;

public sealed record GetDocumentTemplateQuery(Guid TemplateId) : IRequest<DocumentTemplateDto>;

public sealed class GetDocumentTemplateQueryHandler(IDocumentTemplateService templateService, ICurrentUserService currentUser) : IRequestHandler<GetDocumentTemplateQuery, DocumentTemplateDto>
{
    public async Task<DocumentTemplateDto> Handle(GetDocumentTemplateQuery request, CancellationToken cancellationToken)
        => await templateService.GetByIdAsync(currentUser.TenantId!.Value, request.TemplateId, cancellationToken)
           ?? throw new NotFoundException("DocumentTemplate", request.TemplateId);
}

public sealed record GetDocumentTemplatesQuery : IRequest<IReadOnlyList<DocumentTemplateDto>>;

public sealed class GetDocumentTemplatesQueryHandler(IDocumentTemplateService templateService, ICurrentUserService currentUser) : IRequestHandler<GetDocumentTemplatesQuery, IReadOnlyList<DocumentTemplateDto>>
{
    public Task<IReadOnlyList<DocumentTemplateDto>> Handle(GetDocumentTemplatesQuery request, CancellationToken cancellationToken)
        => templateService.GetAllAsync(currentUser.TenantId!.Value, cancellationToken);
}

public sealed record GetSignatureEnvelopeQuery(Guid EnvelopeId) : IRequest<SignatureEnvelopeDto>;

public sealed class GetSignatureEnvelopeQueryHandler(ISignatureService signatureService, ICurrentUserService currentUser) : IRequestHandler<GetSignatureEnvelopeQuery, SignatureEnvelopeDto>
{
    public async Task<SignatureEnvelopeDto> Handle(GetSignatureEnvelopeQuery request, CancellationToken cancellationToken)
        => await signatureService.GetByIdAsync(currentUser.TenantId!.Value, request.EnvelopeId, cancellationToken)
           ?? throw new NotFoundException("SignatureEnvelope", request.EnvelopeId);
}
