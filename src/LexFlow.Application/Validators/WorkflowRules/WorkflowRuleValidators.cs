using System.Text.Json;
using FluentValidation;
using LexFlow.Application.Commands.WorkflowRules;

namespace LexFlow.Application.Validators.WorkflowRules;

public sealed class CreateWorkflowRuleCommandValidator : AbstractValidator<CreateWorkflowRuleCommand>
{
    public CreateWorkflowRuleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(3, 200);
        RuleFor(x => x.TriggerEvent).NotEmpty();
        RuleFor(x => x.ConditionsJson).Must(BeValidJson).WithMessage("conditions must be valid JSON.");
        RuleFor(x => x.ActionsJson).Must(BeValidJson).WithMessage("actions must be valid JSON array.");
    }

    internal static bool BeValidJson(string json)
    {
        try
        {
            JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public sealed class UpdateWorkflowRuleCommandValidator : AbstractValidator<UpdateWorkflowRuleCommand>
{
    public UpdateWorkflowRuleCommandValidator()
    {
        RuleFor(x => x.RuleId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().Length(3, 200);
        RuleFor(x => x.TriggerEvent).NotEmpty();
        RuleFor(x => x.ConditionsJson).Must(CreateWorkflowRuleCommandValidator.BeValidJson).WithMessage("conditions must be valid JSON.");
        RuleFor(x => x.ActionsJson).Must(CreateWorkflowRuleCommandValidator.BeValidJson).WithMessage("actions must be valid JSON array.");
    }
}

public sealed class TestWorkflowRuleCommandValidator : AbstractValidator<TestWorkflowRuleCommand>
{
    public TestWorkflowRuleCommandValidator()
    {
        RuleFor(x => x.RuleId).NotEmpty();
        RuleFor(x => x.SamplePayloadJson).Must(CreateWorkflowRuleCommandValidator.BeValidJson).WithMessage("samplePayload must be valid JSON.");
    }
}
