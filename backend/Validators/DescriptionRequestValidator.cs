using backend.model.RequestModel;
using FluentValidation;

namespace backend.Validators
{
    public class DescriptionRequestValidator : AbstractValidator<DescriptionRequestModel>
    {
        public DescriptionRequestValidator()
        {
            RuleFor(x => x.DescriptionName)
                .NotEmpty().WithMessage("Description name is required");
        }
    }
}
