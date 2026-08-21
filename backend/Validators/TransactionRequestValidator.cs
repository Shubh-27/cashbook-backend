using System;
using System.Globalization;
using backend.model.RequestModels;
using FluentValidation;

namespace backend.Validators
{
    public class TransactionRequestValidator : AbstractValidator<TransactionRequestModel>
    {
        private static readonly string[] IsoDateFormats =
        [
            "yyyy-MM-ddTHH:mm:ss.fffffffK",
            "yyyy-MM-ddTHH:mm:ss.ffffffK",
            "yyyy-MM-ddTHH:mm:ss.fffffK",
            "yyyy-MM-ddTHH:mm:ss.ffffK",
            "yyyy-MM-ddTHH:mm:ss.fffK",
            "yyyy-MM-ddTHH:mm:ss.ffK",
            "yyyy-MM-ddTHH:mm:ss.fK",
            "yyyy-MM-ddTHH:mm:ssK",
            "yyyy-MM-ddTHH:mm:ss.fffffff",
            "yyyy-MM-ddTHH:mm:ss.fff",
            "yyyy-MM-ddTHH:mm:ss"
        ];

        public TransactionRequestValidator()
        {
            // Foreign key field: referential existence should be checked at the service/repository layer, not here
            RuleFor(x => x.AccountSID)
                .NotEmpty().WithMessage("Account is required")
                .MaximumLength(50).WithMessage("Account identifier cannot exceed 50 characters");

            // Foreign key field: referential existence should be checked at the service/repository layer, not here
            RuleFor(x => x.DescriptionSID)
                .NotEmpty().WithMessage("Description identifier cannot be empty")
                .MaximumLength(50).WithMessage("Description identifier cannot exceed 50 characters")
                .When(x => x.DescriptionSID != null);

            RuleFor(x => x.DescriptionName)
                .MaximumLength(100).WithMessage("Description name cannot exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.DescriptionName));

            RuleFor(x => x.Notes)
                .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.Notes));

            RuleFor(x => x.TransactionDate)
                .NotEmpty().WithMessage("Transaction date is required")
                .Must(date => DateTime.TryParseExact(date, IsoDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
                .WithMessage("Transaction date must be a valid ISO 8601 date")
                .MaximumLength(30).WithMessage("Transaction date is not a valid length");

            RuleFor(x => x.Debit)
                .GreaterThanOrEqualTo(0).WithMessage("Debit amount must be greater than or equal to 0")
                .When(x => x.Debit.HasValue);

            RuleFor(x => x.Credit)
                .GreaterThanOrEqualTo(0).WithMessage("Credit amount must be greater than or equal to 0")
                .When(x => x.Credit.HasValue);

            RuleFor(x => x)
                .Must(x => (x.Debit ?? 0) > 0 || (x.Credit ?? 0) > 0)
                .WithMessage("Amount must be greater than 0")
                .WithName("Amount");

            RuleFor(x => x)
                .Must(x => !((x.Debit ?? 0) > 0 && (x.Credit ?? 0) > 0))
                .WithMessage("A transaction cannot have both Debit and Credit amounts")
                .WithName("Amount");
        }
    }
}
