using backend.model.RequestModel;
using FluentValidation;

namespace backend.Validators
{
    public class AccountRequestValidator : AbstractValidator<AccountRequestModel>
    {
        public AccountRequestValidator()
        {
            RuleFor(x => x.AccountName)
                .NotEmpty().WithMessage("Account name is required")
                .MinimumLength(3).WithMessage("Account name must be at least 3 characters");

            RuleFor(x => x.AccountNumber)
                .Matches(@"^[0-9]*$").WithMessage("Account number must be numeric")
                .When(x => !string.IsNullOrEmpty(x.AccountNumber));
        }
    }
}
