using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.TaxRates;

/// <summary>Module 15 §8 Taxes CRUD (PRD §17).</summary>
public sealed record CreateTaxRateCommand(string CountryCode, string TaxType, string ComponentsJson, Guid? BranchId) : IRequest<TaxRateDto>;

public sealed class CreateTaxRateCommandHandler(ITaxRateService taxRateService, ICurrentUserService currentUser) : IRequestHandler<CreateTaxRateCommand, TaxRateDto>
{
    public Task<TaxRateDto> Handle(CreateTaxRateCommand request, CancellationToken cancellationToken)
        => taxRateService.CreateAsync(currentUser.TenantId!.Value, request.CountryCode, request.TaxType, request.ComponentsJson, request.BranchId, cancellationToken);
}

public sealed record UpdateTaxRateCommand(Guid Id, string CountryCode, string TaxType, string ComponentsJson, bool IsActive) : IRequest<TaxRateDto>;

public sealed class UpdateTaxRateCommandHandler(ITaxRateService taxRateService, ICurrentUserService currentUser) : IRequestHandler<UpdateTaxRateCommand, TaxRateDto>
{
    public Task<TaxRateDto> Handle(UpdateTaxRateCommand request, CancellationToken cancellationToken)
        => taxRateService.UpdateAsync(currentUser.TenantId!.Value, request.Id, request.CountryCode, request.TaxType, request.ComponentsJson, request.IsActive, cancellationToken);
}

public sealed record DeleteTaxRateCommand(Guid Id) : IRequest;

public sealed class DeleteTaxRateCommandHandler(ITaxRateService taxRateService, ICurrentUserService currentUser) : IRequestHandler<DeleteTaxRateCommand>
{
    public async Task Handle(DeleteTaxRateCommand request, CancellationToken cancellationToken)
        => await taxRateService.DeleteAsync(currentUser.TenantId!.Value, request.Id, cancellationToken);
}
