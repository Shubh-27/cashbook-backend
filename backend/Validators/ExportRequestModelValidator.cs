using backend.model.RequestModel;
using FluentValidation;

namespace backend.Validators
{
    public class ExportRequestModelValidator : AbstractValidator<ExportRequestModel>
    {
        public ExportRequestModelValidator()
        {
            Include(new SearchRequestModelValidator());
        }
    }
}
