using backend.model.RequestModel;
using FluentValidation;

namespace backend.Validators
{
    public class ImportProfileRuleRequestValidator : AbstractValidator<ImportProfileRuleRequest>
    {
        public ImportProfileRuleRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Profile name is required")
                .MaximumLength(100).WithMessage("Profile name cannot exceed 100 characters");

            RuleFor(x => x.HeaderRow)
                .GreaterThanOrEqualTo(1).WithMessage("Header row must be at least 1");

            RuleFor(x => x.EndRow)
                .GreaterThan(x => x.HeaderRow).WithMessage("End row must be after header row");
        }
    }
}
