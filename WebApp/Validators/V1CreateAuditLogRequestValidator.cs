using Common;
using FluentValidation;
using Models.Dto.V1.Requests;

namespace WebApp.Controllers.V1;

public class V1CreateAuditLogRequestValidator : AbstractValidator<V1CreateAuditLogRequest>
{
    public V1CreateAuditLogRequestValidator()
    {
        RuleFor(x => x.Orders).NotEmpty();
        RuleForEach(x => x.Orders)
            .NotNull();
        RuleForEach(x => x.Orders)
            .ChildRules(order =>
            {
                order.RuleFor(o => o.OrderId)
                    .GreaterThan(0)
                    .WithMessage("OrderId must be greater than 0");
                order.RuleFor(o => o.OrderItemId)
                    .GreaterThan(0)
                    .WithMessage("OrderItemId must be greater than 0");
                order.RuleFor(o => o.CustomerId)
                    .GreaterThan(0)
                    .WithMessage("CustomerId must be greater than 0");
                order.RuleFor(o => o.OrderStatus)
                    .NotEmpty().IsEnumName(typeof(OrderStatus)).WithMessage("OrderStatus must not be empty");
            });
    }
}