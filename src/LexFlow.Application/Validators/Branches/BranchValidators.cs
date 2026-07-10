using FluentValidation;
using LexFlow.Application.Commands.Branches;

namespace LexFlow.Application.Validators.Branches;

public sealed class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Code).NotEmpty();
        RuleFor(x => x.Tz).NotEmpty();
    }
}

public sealed class UpdateBranchCommandValidator : AbstractValidator<UpdateBranchCommand>
{
    public UpdateBranchCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Code).NotEmpty();
        RuleFor(x => x.Tz).NotEmpty();
    }
}
