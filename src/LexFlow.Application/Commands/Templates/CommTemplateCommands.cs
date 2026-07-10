using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Templates;

/// <summary>Module 15 §11 Email/SMS/WhatsApp Templates CRUD (PRD §17).</summary>
public sealed record CreateCommTemplateCommand(string Channel, string Name, string Body, string VariablesJson) : IRequest<CommTemplateDto>;

public sealed class CreateCommTemplateCommandHandler(ICommTemplateService commTemplateService, ICurrentUserService currentUser)
    : IRequestHandler<CreateCommTemplateCommand, CommTemplateDto>
{
    public Task<CommTemplateDto> Handle(CreateCommTemplateCommand request, CancellationToken cancellationToken)
        => commTemplateService.CreateAsync(currentUser.TenantId!.Value, request.Channel, request.Name, request.Body, request.VariablesJson, cancellationToken);
}

public sealed record UpdateCommTemplateCommand(Guid Id, string Body, string VariablesJson, bool IsActive) : IRequest<CommTemplateDto>;

public sealed class UpdateCommTemplateCommandHandler(ICommTemplateService commTemplateService, ICurrentUserService currentUser)
    : IRequestHandler<UpdateCommTemplateCommand, CommTemplateDto>
{
    public Task<CommTemplateDto> Handle(UpdateCommTemplateCommand request, CancellationToken cancellationToken)
        => commTemplateService.UpdateAsync(currentUser.TenantId!.Value, request.Id, request.Body, request.VariablesJson, request.IsActive, cancellationToken);
}

public sealed record DeleteCommTemplateCommand(Guid Id) : IRequest;

public sealed class DeleteCommTemplateCommandHandler(ICommTemplateService commTemplateService, ICurrentUserService currentUser) : IRequestHandler<DeleteCommTemplateCommand>
{
    public async Task Handle(DeleteCommTemplateCommand request, CancellationToken cancellationToken)
        => await commTemplateService.DeleteAsync(currentUser.TenantId!.Value, request.Id, cancellationToken);
}
