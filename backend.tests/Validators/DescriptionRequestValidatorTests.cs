using backend.model.RequestModels;
using backend.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace backend.tests.Validators
{
    public class DescriptionRequestValidatorTests
    {
        private readonly DescriptionRequestValidator _validator = new();

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void DescriptionName_WhenNullOrEmpty_FailsWithRequiredMessage(string? descriptionName)
        {
            var model = new DescriptionRequestModel { DescriptionName = descriptionName! };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.DescriptionName)
                  .WithErrorMessage("Description name is required");
        }

        [Fact]
        public void DescriptionName_WhenLongerThan100Characters_FailsWithMaxLengthMessage()
        {
            var model = new DescriptionRequestModel { DescriptionName = new string('D', 101) };
            var result = _validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.DescriptionName)
                  .WithErrorMessage("Description name cannot exceed 100 characters");
        }

        [Theory]
        [InlineData("Groceries")]
        [InlineData("Monthly Electricity Bill")]
        public void DescriptionName_WhenValidLength_PassesValidation(string descriptionName)
        {
            var model = new DescriptionRequestModel { DescriptionName = descriptionName };
            var result = _validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.DescriptionName);
        }
    }
}
