using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.RateCards;

public sealed record CreateRateCardCommand(string Name, Guid? BranchId, bool IsDefault) : IRequest<RateCardDto>;

public sealed class CreateRateCardCommandHandler(IRateCardService service, ICurrentUserService currentUser) : IRequestHandler<CreateRateCardCommand, RateCardDto>
{
    public Task<RateCardDto> Handle(CreateRateCardCommand request, CancellationToken cancellationToken)
        => service.CreateRateCardAsync(currentUser.TenantId!.Value, request.Name, request.BranchId, request.IsDefault, cancellationToken);
}

public sealed record UpsertRateCardEntryCommand(Guid RateCardId, string? Role, Guid? UserId, decimal Rate, string Currency, DateOnly EffectiveFrom) : IRequest<RateCardEntryDto>;

public sealed class UpsertRateCardEntryCommandHandler(IRateCardService service, ICurrentUserService currentUser) : IRequestHandler<UpsertRateCardEntryCommand, RateCardEntryDto>
{
    public Task<RateCardEntryDto> Handle(UpsertRateCardEntryCommand request, CancellationToken cancellationToken)
        => service.UpsertRateCardEntryAsync(currentUser.TenantId!.Value, request.RateCardId, request.Role, request.UserId, request.Rate, request.Currency, request.EffectiveFrom, cancellationToken);
}

public sealed record SetBillingArrangementCommand(
    Guid MatterId, string ArrangementType, Guid? RateCardId, decimal? FixedAmount, string? MilestonesJson,
    decimal? RetainerAmount, string? RetainerPeriod, int? AutoInvoiceDay, decimal? ReplenishmentThreshold, decimal? ContingencyPct) : IRequest<BillingArrangementDto>;

public sealed class SetBillingArrangementCommandHandler(IRateCardService service, ICurrentUserService currentUser) : IRequestHandler<SetBillingArrangementCommand, BillingArrangementDto>
{
    public Task<BillingArrangementDto> Handle(SetBillingArrangementCommand request, CancellationToken cancellationToken)
        => service.SetBillingArrangementAsync(currentUser.TenantId!.Value, request.MatterId, new SetBillingArrangementInput(
            request.ArrangementType, request.RateCardId, request.FixedAmount, request.MilestonesJson, request.RetainerAmount,
            request.RetainerPeriod, request.AutoInvoiceDay, request.ReplenishmentThreshold, request.ContingencyPct), cancellationToken);
}
