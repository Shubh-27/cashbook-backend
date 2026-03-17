using backend.model.RequestModel;
using FluentValidation;

namespace backend.Validators
{
    public class TransactionRequestValidator : AbstractValidator<TransactionRequestModel>
    {
        public TransactionRequestValidator()
        {
            RuleFor(x => x.AccountSID)
                .NotEmpty().WithMessage("Account is required");

            RuleFor(x => x.TransactionDate)
                .NotEmpty().WithMessage("Transaction date is required");

            RuleFor(x => x)
                .Must(x => (x.Debit ?? 0) > 0 || (x.Credit ?? 0) > 0)
                .WithMessage("Amount must be greater than 0")
                .WithName("Amount");
        }
    }
}
