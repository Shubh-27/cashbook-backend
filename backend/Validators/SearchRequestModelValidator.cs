using backend.common.Models;
using backend.common;
using FluentValidation;

namespace backend.Validators
{
    public class SearchRequestModelValidator : AbstractValidator<SearchRequestModel>
    {
        public SearchRequestModelValidator()
        {
            RuleFor(x => x.Page)
                .GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1");

            RuleFor(x => x.PageSize)
                .Must(ps => ps == -1 || ps >= 1).WithMessage("Page size must be at least 1 or -1 for all");

            RuleFor(x => x.SortOrder)
                .Must(order => string.IsNullOrEmpty(order) ||
                               order.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
                               order.Equals("desc", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Sort order must be 'asc' or 'desc'");
        }
    }
}
