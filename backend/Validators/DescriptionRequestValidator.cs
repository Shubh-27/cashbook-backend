using backend.model.RequestModels;
using FluentValidation;

namespace backend.Validators
{
    public class DescriptionRequestValidator : AbstractValidator<DescriptionRequestModel>
    {
        public DescriptionRequestValidator()
        {
            RuleFor(x => x.DescriptionName)
                .NotEmpty().WithMessage("Description name is required")
                .MaximumLength(100).WithMessage("Description name cannot exceed 100 characters");
        }
    }
}
