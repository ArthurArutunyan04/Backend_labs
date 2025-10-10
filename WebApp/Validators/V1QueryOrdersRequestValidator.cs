using FluentValidation;
using Models.Dto.V1.Requests; 

namespace WebApi.Validators
{
    public class V1QueryOrdersRequestValidator: AbstractValidator<V1QueryOrdersRequest>
    {
        public V1QueryOrdersRequestValidator()
        {
            RuleForEach(x => x.Ids)
                .GreaterThan(0).WithMessage("ID must be positive");

            RuleForEach(x => x.CustomerIds)
                .GreaterThan(0).WithMessage("Customer ID must be positive");

            RuleFor(x => x.Page)
                .GreaterThan(0).WithMessage("Page must be greater than 0")
                .When(x => x.Page is not null);

            RuleFor(x => x.PageSize)
                .GreaterThan(0).WithMessage("Page size must be greater than 0")
                .When(x => x.PageSize is not null);

            RuleFor(x => x)
                .Must(x => x.Ids?.Length > 0 || x.CustomerIds?.Length > 0)
                .WithMessage("Either IDs or Customer IDs must be provided");
        }
    }
}