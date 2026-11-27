using backend.model.RequestModel;
using FluentValidation;

namespace backend.Validators
{
    public class CommitStatementImportRequestValidator : AbstractValidator<CommitStatementImportRequest>
    {
        public CommitStatementImportRequestValidator()
        {
            RuleFor(x => x.AccountSID)
                .NotEmpty().WithMessage("Account is required")
                .MaximumLength(50).WithMessage("Account identifier cannot exceed 50 characters");

            RuleFor(x => x.FileName)
                .NotEmpty().WithMessage("File name is required");

            RuleFor(x => x.FileType)
                .NotEmpty().WithMessage("File type is required");

            RuleFor(x => x.Rows)
                .NotEmpty().WithMessage("At least one row is required");
        }
    }
}
