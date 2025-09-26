using FluentValidation;
using Models.Dto.V1.Requests; 

namespace WebApi.Validators
{
    public class V1QueryOrdersRequestValidator : AbstractValidator<V1QueryOrdersRequest>
    {
        public V1QueryOrdersRequestValidator()
        {
            RuleFor(x => x.CustomerIds)
                .NotNull().WithMessage("CustomerIds cannot be null")
                .NotEmpty().WithMessage("CustomerIds cannot be empty")
                .Must(x => x.All(id => id > 0)).WithMessage("Each CustomerId must be greater than 0");

            RuleFor(x => x.Ids)
                .NotEmpty().WithMessage("Ids cannot be empty if provided")
                .When(x => x.Ids != null);

            RuleForEach(x => x.Ids)
                .GreaterThan(0).WithMessage("Each Id must be greater than 0")
                .When(x => x.Ids != null);

            RuleFor(x => x.Page)
                .GreaterThanOrEqualTo(0).WithMessage("Page must be greater than or equal to 0")
                .When(x => x.Page.HasValue);

            RuleFor(x => x.PageSize)
                .GreaterThan(0).WithMessage("PageSize must be greater than 0")
                .When(x => x.PageSize.HasValue);
        }
    }
}