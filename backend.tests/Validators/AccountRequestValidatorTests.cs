using backend.model.RequestModels;
using backend.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace backend.tests.Validators
{
    public class AccountRequestValidatorTests
    {
        private readonly AccountRequestValidator _validator = new();

        #region AccountName Rules
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void AccountName_WhenNullOrEmpty_FailsWithRequiredMessage(string? accountName)
        {
            var model = new AccountRequestModel { AccountName = accountName! };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.AccountName)
                  .WithErrorMessage("Account name is required");
        }

        [Theory]
        [InlineData("a")]
        [InlineData("ab")]
        public void AccountName_WhenShorterThan3Characters_FailsWithMinLengthMessage(string accountName)
        {
            var model = new AccountRequestModel { AccountName = accountName };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.AccountName)
                  .WithErrorMessage("Account name must be at least 3 characters");
        }

        [Fact]
        public void AccountName_WhenLongerThan100Characters_FailsWithMaxLengthMessage()
        {
            var model = new AccountRequestModel { AccountName = new string('A', 101) };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.AccountName)
                  .WithErrorMessage("Account name cannot exceed 100 characters");
        }

        [Theory]
        [InlineData("Checking")]
        [InlineData("My Main Bank Account")]
        public void AccountName_WhenValidLength_PassesValidation(string accountName)
        {
            var model = new AccountRequestModel { AccountName = accountName };
            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.AccountName);
        }
        #endregion

        #region BankName Rules
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void BankName_WhenNullOrEmpty_PassesValidation(string? bankName)
        {
            var model = new AccountRequestModel
            {
                AccountName = "Checking Account",
                BankName = bankName
            };
            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.BankName);
        }

        [Fact]
        public void BankName_WhenLongerThan100Characters_FailsWithMaxLengthMessage()
        {
            var model = new AccountRequestModel
            {
                AccountName = "Checking Account",
                BankName = new string('B', 101)
            };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.BankName)
                  .WithErrorMessage("Bank name cannot exceed 100 characters");
        }

        [Fact]
        public void BankName_WhenValidLength_PassesValidation()
        {
            var model = new AccountRequestModel
            {
                AccountName = "Checking Account",
                BankName = "Chase Bank"
            };
            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.BankName);
        }
        #endregion

        #region AccountNumber Rules
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void AccountNumber_WhenNullOrEmpty_PassesValidation(string? accountNumber)
        {
            var model = new AccountRequestModel
            {
                AccountName = "Checking Account",
                AccountNumber = accountNumber
            };
            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.AccountNumber);
        }

        [Theory]
        [InlineData("1234abcd")]
        [InlineData("12-34-56")]
        [InlineData("ACC1234")]
        public void AccountNumber_WhenNonNumeric_FailsWithNumericMessage(string accountNumber)
        {
            var model = new AccountRequestModel
            {
                AccountName = "Checking Account",
                AccountNumber = accountNumber
            };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.AccountNumber)
                  .WithErrorMessage("Account number must be numeric");
        }

        [Fact]
        public void AccountNumber_WhenExceedsLongRange_FailsWithTooLargeMessage()
        {
            // 99999999999999999999 exceeds long.MaxValue (9223372036854775807)
            var model = new AccountRequestModel
            {
                AccountName = "Checking Account",
                AccountNumber = "99999999999999999999"
            };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.AccountNumber)
                  .WithErrorMessage("Account number is too large");
        }

        [Theory]
        [InlineData("12345678")]
        [InlineData("987654321012345")]
        public void AccountNumber_WhenNumericAndValidRange_PassesValidation(string accountNumber)
        {
            var model = new AccountRequestModel
            {
                AccountName = "Checking Account",
                AccountNumber = accountNumber
            };
            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.AccountNumber);
        }
        #endregion
    }
}
