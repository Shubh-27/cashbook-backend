using backend.model.RequestModel;
using FluentValidation;

namespace backend.Validators
{
    public class SaveImportProfileRequestValidator : AbstractValidator<SaveImportProfileRequest>
    {
        public SaveImportProfileRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Profile name is required")
                .MaximumLength(100).WithMessage("Profile name cannot exceed 100 characters");

            RuleFor(x => x.FileType)
                .NotEmpty().WithMessage("File type is required");

            RuleFor(x => x.HeaderRow)
                .GreaterThanOrEqualTo(1).WithMessage("Header row must be at least 1");
        }
    }
}
