using FluentValidation;
using Models.Dto.V1.Requests;

namespace WebApp.Validators
{
    public class V1QueryOrdersRequestValidator : AbstractValidator<V1QueryOrdersRequest>
    {
        public V1QueryOrdersRequestValidator()
        {
            // Правило для Page - должно быть положительным числом, если указано
            RuleFor(x => x.Page)
                .GreaterThan(0)
                .When(x => x.Page.HasValue)
                .WithMessage("Page must be greater than 0");

            // Правило для PageSize - должно быть в допустимом диапазоне, если указано
            RuleFor(x => x.PageSize)
                .InclusiveBetween(1, 1000) // Ограничиваем максимальный размер страницы
                .When(x => x.PageSize.HasValue)
                .WithMessage("PageSize must be between 1 and 1000");

            // Правило: если указан PageSize, то должен быть указан и Page
            RuleFor(x => x)
                .Must(x => !(x.PageSize.HasValue && !x.Page.HasValue))
                .WithMessage("Page must be specified when PageSize is provided");

            // Правило: если указан Page, то должен быть указан и PageSize
            RuleFor(x => x)
                .Must(x => !(x.Page.HasValue && !x.PageSize.HasValue))
                .WithMessage("PageSize must be specified when Page is provided");

            // Правило для Ids - не должно содержать отрицательных значений или нулей
            RuleFor(x => x.Ids)
                .Must(ids => ids == null || ids.All(id => id > 0))
                .WithMessage("All IDs must be greater than 0");

            // Правило для CustomerIds - не должно содержать отрицательных значений или нулей
            RuleFor(x => x.CustomerIds)
                .Must(customerIds => customerIds == null || customerIds.All(id => id > 0))
                .WithMessage("All CustomerIds must be greater than 0");

            // Правило: должен быть указан хотя бы один критерий поиска
            RuleFor(x => x)
                .Must(x => HasValidSearchCriteria(x))
                .WithMessage("At least one search criterion must be specified (Ids, CustomerIds, or pagination parameters)");

            // Правило: максимальное количество ID для поиска
            RuleFor(x => x.Ids)
                .Must(ids => ids == null || ids.Length <= 1000)
                .WithMessage("Maximum 1000 IDs allowed for search");

            // Правило: максимальное количество CustomerIds для поиска
            RuleFor(x => x.CustomerIds)
                .Must(customerIds => customerIds == null || customerIds.Length <= 1000)
                .WithMessage("Maximum 1000 CustomerIds allowed for search");
        }

        private bool HasValidSearchCriteria(V1QueryOrdersRequest request)
        {
            // Проверяем, что указан хотя бы один критерий поиска
            return (request.Ids != null && request.Ids.Length > 0) ||
                   (request.CustomerIds != null && request.CustomerIds.Length > 0) ||
                   (request.Page.HasValue && request.PageSize.HasValue);
        }
    }
}
