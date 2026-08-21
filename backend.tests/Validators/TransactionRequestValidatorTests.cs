using backend.model.RequestModels;
using backend.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace backend.tests.Validators
{
    public class TransactionRequestValidatorTests
    {
        private readonly TransactionRequestValidator _validator = new();

        private static TransactionRequestModel CreateValidModel()
        {
            return new TransactionRequestModel
            {
                AccountSID = "acc-valid-123",
                TransactionDate = "2026-08-21T10:30:00Z",
                Debit = 50.00,
                Credit = null,
                DescriptionName = "Coffee"
            };
        }

        #region AccountSID Rules
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void AccountSID_WhenNullOrEmpty_FailsWithRequiredMessage(string? accountSid)
        {
            var model = CreateValidModel();
            model.AccountSID = accountSid!;

            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.AccountSID)
                  .WithErrorMessage("Account is required");
        }

        [Fact]
        public void AccountSID_WhenLongerThan50Characters_FailsWithMaxLengthMessage()
        {
            var model = CreateValidModel();
            model.AccountSID = new string('A', 51);

            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.AccountSID)
                  .WithErrorMessage("Account identifier cannot exceed 50 characters");
        }

        [Fact]
        public void AccountSID_WhenValid_PassesValidation()
        {
            var model = CreateValidModel();
            model.AccountSID = "valid-account-sid-12345";

            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.AccountSID);
        }
        #endregion

        #region DescriptionSID Rules
        [Fact]
        public void DescriptionSID_WhenNull_PassesValidation()
        {
            var model = CreateValidModel();
            model.DescriptionSID = null;

            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.DescriptionSID);
        }

        [Fact]
        public void DescriptionSID_WhenEmptyString_FailsWithCannotBeEmptyMessage()
        {
            var model = CreateValidModel();
            model.DescriptionSID = "";

            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.DescriptionSID)
                  .WithErrorMessage("Description identifier cannot be empty");
        }

        [Fact]
        public void DescriptionSID_WhenLongerThan50Characters_FailsWithMaxLengthMessage()
        {
            var model = CreateValidModel();
            model.DescriptionSID = new string('D', 51);

            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.DescriptionSID)
                  .WithErrorMessage("Description identifier cannot exceed 50 characters");
        }

        [Fact]
        public void DescriptionSID_WhenValid_PassesValidation()
        {
            var model = CreateValidModel();
            model.DescriptionSID = "desc-sid-12345";

            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.DescriptionSID);
        }
        #endregion

        #region DescriptionName Rules
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void DescriptionName_WhenNullOrEmpty_PassesValidation(string? descName)
        {
            var model = CreateValidModel();
            model.DescriptionName = descName;

            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.DescriptionName);
        }

        [Fact]
        public void DescriptionName_WhenLongerThan100Characters_FailsWithMaxLengthMessage()
        {
            var model = CreateValidModel();
            model.DescriptionName = new string('N', 101);

            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.DescriptionName)
                  .WithErrorMessage("Description name cannot exceed 100 characters");
        }

        [Fact]
        public void DescriptionName_WhenValid_PassesValidation()
        {
            var model = CreateValidModel();
            model.DescriptionName = "Starbucks Coffee";

            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.DescriptionName);
        }
        #endregion

        #region Notes Rules
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Notes_WhenNullOrEmpty_PassesValidation(string? notes)
        {
            var model = CreateValidModel();
            model.Notes = notes;

            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.Notes);
        }

        [Fact]
        public void Notes_WhenLongerThan500Characters_FailsWithMaxLengthMessage()
        {
            var model = CreateValidModel();
            model.Notes = new string('N', 501);

            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.Notes)
                  .WithErrorMessage("Notes cannot exceed 500 characters");
        }

        [Fact]
        public void Notes_WhenValid_PassesValidation()
        {
            var model = CreateValidModel();
            model.Notes = "Meeting with client for project kickoff";

            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.Notes);
        }
        #endregion

        #region TransactionDate Rules
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void TransactionDate_WhenNullOrEmpty_FailsWithRequiredMessage(string? date)
        {
            var model = CreateValidModel();
            model.TransactionDate = date!;

            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.TransactionDate)
                  .WithErrorMessage("Transaction date is required");
        }

        [Theory]
        [InlineData("not-a-date")]
        [InlineData("2026/08/21")]
        [InlineData("21-08-2026")]
        public void TransactionDate_WhenInvalidIsoFormat_FailsWithIsoDateMessage(string date)
        {
            var model = CreateValidModel();
            model.TransactionDate = date;

            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.TransactionDate)
                  .WithErrorMessage("Transaction date must be a valid ISO 8601 date");
        }

        [Fact]
        public void TransactionDate_WhenLongerThan30Characters_FailsWithLengthMessage()
        {
            var model = CreateValidModel();
            model.TransactionDate = "2026-08-21T10:30:00.12345678901234567890Z";

            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.TransactionDate)
                  .WithErrorMessage("Transaction date is not a valid length");
        }

        [Theory]
        [InlineData("2026-08-21T10:30:00Z")]
        [InlineData("2026-08-21T10:30:00.000Z")]
        [InlineData("2026-08-21T10:30:00+05:30")]
        public void TransactionDate_WhenValidIso8601Date_PassesValidation(string date)
        {
            var model = CreateValidModel();
            model.TransactionDate = date;

            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.TransactionDate);
        }
        #endregion

        #region Debit & Credit Rules
        [Fact]
        public void Debit_WhenNegative_FailsWithGreaterThanOrEqualToZeroMessage()
        {
            var model = CreateValidModel();
            model.Debit = -10.00;
            model.Credit = null;

            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.Debit)
                  .WithErrorMessage("Debit amount must be greater than or equal to 0");
        }

        [Fact]
        public void Credit_WhenNegative_FailsWithGreaterThanOrEqualToZeroMessage()
        {
            var model = CreateValidModel();
            model.Debit = null;
            model.Credit = -50.00;

            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.Credit)
                  .WithErrorMessage("Credit amount must be greater than or equal to 0");
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData(0.0, null)]
        [InlineData(null, 0.0)]
        [InlineData(0.0, 0.0)]
        public void Amount_WhenBothDebitAndCreditZeroOrNull_FailsWithAmountGreaterThanZeroMessage(double? debit, double? credit)
        {
            var model = CreateValidModel();
            model.Debit = debit;
            model.Credit = credit;

            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor("Amount")
                  .WithErrorMessage("Amount must be greater than 0");
        }

        [Fact]
        public void Amount_WhenBothDebitAndCreditGreaterThanZero_FailsWithMutualExclusivityMessage()
        {
            var model = CreateValidModel();
            model.Debit = 50.00;
            model.Credit = 100.00;

            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor("Amount")
                  .WithErrorMessage("A transaction cannot have both Debit and Credit amounts");
        }

        [Fact]
        public void Amount_WhenDebitOnly_PassesValidation()
        {
            var model = CreateValidModel();
            model.Debit = 75.00;
            model.Credit = null;

            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.Debit);
            result.ShouldNotHaveValidationErrorFor(x => x.Credit);
            result.ShouldNotHaveValidationErrorFor("Amount");
        }

        [Fact]
        public void Amount_WhenCreditOnly_PassesValidation()
        {
            var model = CreateValidModel();
            model.Debit = null;
            model.Credit = 120.00;

            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.Debit);
            result.ShouldNotHaveValidationErrorFor(x => x.Credit);
            result.ShouldNotHaveValidationErrorFor("Amount");
        }
        #endregion
    }
}
