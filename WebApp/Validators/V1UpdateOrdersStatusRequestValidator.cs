using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Common;
using Models.Dto.V1.Requests;

namespace WebApp.Validators
{
    public class V1UpdateOrdersStatusRequestValidator : AbstractValidator<V1UpdateOrdersStatusRequest>
    {
        private static readonly string[] AllowedStatusNames = Enum.GetNames<OrderStatus>();
        private static readonly HashSet<string> AllowedStatuses = AllowedStatusNames
            .Select(x => x.ToLowerInvariant())
            .ToHashSet();

        public V1UpdateOrdersStatusRequestValidator()
        {
            RuleFor(x => x.OrderIds)
                .NotNull().WithMessage("OrderIds is required")
                .Must(ids => ids.Length > 0).WithMessage("OrderIds must contain at least one id")
                .Must(ids => ids.All(id => id > 0)).WithMessage("OrderIds must be greater than zero");

            RuleFor(x => x.NewStatus)
                .NotEmpty().WithMessage("NewStatus is required")
                .Must(status => !string.IsNullOrWhiteSpace(status) && AllowedStatuses.Contains(status.ToLowerInvariant()))
                .WithMessage($"NewStatus must be one of: {string.Join(", ", AllowedStatusNames)}");
        }
    }
}

