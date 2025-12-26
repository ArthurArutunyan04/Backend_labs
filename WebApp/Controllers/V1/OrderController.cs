using Microsoft.AspNetCore.Mvc;
using Models.Dto.V1.Requests;
using Models.Dto.V1.Responses;
using WebApp.BLL.Models;
using WebApp.BLL.Services;
using WebApp.Validators;

[Route("api/v1/order")]
public class OrderController(OrderService orderService, ValidatorFactory validatorFactory) : ControllerBase
{
    [HttpPost("batch-create")]
    public async Task<ActionResult<V1CreateOrderResponse>> V1BatchCreate([FromBody] V1CreateOrderRequest request, CancellationToken token)
    {
        var validationResult = await validatorFactory.GetValidator<V1CreateOrderRequest>().ValidateAsync(request, token);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.ToDictionary());
        }

        var res = await orderService.BatchInsert(request.Orders.Select(x => new OrderUnit
        {
            CustomerId = x.CustomerId,
            DeliveryAddress = x.DeliveryAddress,
            TotalPriceCents = x.TotalPriceCents,
            TotalPriceCurrency = x.TotalPriceCurrency,
            OrderItems = x.OrderItems.Select(p => new OrderItemUnit
            {
                ProductId = p.ProductId,
                Quantity = p.Quantity,
                ProductTitle = p.ProductTitle,
                ProductUrl = p.ProductUrl,
                PriceCents = p.PriceCents,
                PriceCurrency = p.PriceCurrency,
            }).ToArray()
        }).ToArray(), token);

        return Ok(new V1CreateOrderResponse
        {
            Orders = Map(res)
        });
    }

    [HttpPost("query")]
    public async Task<ActionResult<V1QueryOrdersResponse>> V1QueryOrders([FromBody] V1QueryOrdersRequest request, CancellationToken token)
    {
        var validationResult = await validatorFactory.GetValidator<V1QueryOrdersRequest>().ValidateAsync(request, token);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.ToDictionary());
        }

        var res = await orderService.GetOrders(new QueryOrderItemsModel
        {
            Ids = request.Ids,
            CustomerIds = request.CustomerIds,
            Page = request.Page ?? 0,
            PageSize = request.PageSize ?? 0,
            IncludeOrderItems = request.IncludeOrderItems
        }, token);

        return Ok(new V1QueryOrdersResponse
        {
            Orders = Map(res)
        });
    }

    [HttpPut("status")]
    [ProducesResponseType(typeof(V1UpdateOrderStatusResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<V1UpdateOrderStatusResponse>> UpdateOrdersStatus(
        [FromBody] V1UpdateOrdersStatusRequest request,
        CancellationToken token)
    {
        try
        {
            await orderService.UpdateOrdersStatusAsync(request.OrderIds, request.NewStatus, token);
            return Ok(new V1UpdateOrderStatusResponse());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private Models.Dto.Common.OrderUnit[] Map(OrderUnit[] orders)
    {
        return orders.Select(x => new Models.Dto.Common.OrderUnit
        {
            Id = x.Id,
            CustomerId = x.CustomerId,
            DeliveryAddress = x.DeliveryAddress,
            TotalPriceCents = x.TotalPriceCents,
            TotalPriceCurrency = x.TotalPriceCurrency,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt,
            OrderItems = x.OrderItems.Select(p => new Models.Dto.Common.OrderItemUnit
            {
                Id = p.Id,
                OrderId = p.OrderId,
                ProductId = p.ProductId,
                Quantity = p.Quantity,
                ProductTitle = p.ProductTitle,
                ProductUrl = p.ProductUrl,
                PriceCents = p.PriceCents,
                PriceCurrency = p.PriceCurrency,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            }).ToArray()
        }).ToArray();
    }
}