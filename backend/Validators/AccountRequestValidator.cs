using backend.model.RequestModels;
using FluentValidation;

namespace backend.Validators
{
    public class AccountRequestValidator : AbstractValidator<AccountRequestModel>
    {
        public AccountRequestValidator()
        {
            RuleFor(x => x.AccountName)
                .NotEmpty().WithMessage("Account name is required")
                .MinimumLength(3).WithMessage("Account name must be at least 3 characters")
                .MaximumLength(100).WithMessage("Account name cannot exceed 100 characters");

            RuleFor(x => x.BankName)
                .MaximumLength(100).WithMessage("Bank name cannot exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.BankName));

            RuleFor(x => x.AccountNumber)
                .Matches(@"^[0-9]*$").WithMessage("Account number must be numeric")
                .Must(val => long.TryParse(val, out _)).WithMessage("Account number is too large")
                .When(x => !string.IsNullOrEmpty(x.AccountNumber));
        }
    }
}
