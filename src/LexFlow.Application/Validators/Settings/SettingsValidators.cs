using FluentValidation;
using LexFlow.Application.Commands.NumberSeries;
using LexFlow.Application.Commands.Settings;
using LexFlow.Application.Commands.TaxRates;
using LexFlow.Application.Commands.Templates;

namespace LexFlow.Application.Validators.Settings;

public sealed class UpdateSettingsSectionCommandValidator : AbstractValidator<UpdateSettingsSectionCommand>
{
    public UpdateSettingsSectionCommandValidator()
    {
        RuleFor(x => x.Section).NotEmpty();
        RuleFor(x => x.ValueJson).NotEmpty();
    }
}

public sealed class CreateNumberSeriesCommandValidator : AbstractValidator<CreateNumberSeriesCommand>
{
    public CreateNumberSeriesCommandValidator()
    {
        RuleFor(x => x.SeriesKey).NotEmpty();
        RuleFor(x => x.FiscalYear).GreaterThan(2000);
        RuleFor(x => x.FormatPattern).NotEmpty().Must(p => p.Contains("{seq", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Number series pattern must contain a {seq} token (PRD Module 15 Validation).");
    }
}

public sealed class UpdateNumberSeriesPatternCommandValidator : AbstractValidator<UpdateNumberSeriesPatternCommand>
{
    public UpdateNumberSeriesPatternCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.FormatPattern).NotEmpty().Must(p => p.Contains("{seq", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Number series pattern must contain a {seq} token (PRD Module 15 Validation).");
    }
}

public sealed class CreateTaxRateCommandValidator : AbstractValidator<CreateTaxRateCommand>
{
    public CreateTaxRateCommandValidator()
    {
        RuleFor(x => x.CountryCode).NotEmpty();
        RuleFor(x => x.TaxType).NotEmpty();
        RuleFor(x => x.ComponentsJson).NotEmpty();
    }
}

public sealed class CreateCommTemplateCommandValidator : AbstractValidator<CreateCommTemplateCommand>
{
    public CreateCommTemplateCommandValidator()
    {
        RuleFor(x => x.Channel).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Body).NotEmpty();
    }
}
