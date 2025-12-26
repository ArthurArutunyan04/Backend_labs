using DefaultNamespace;
using DefaultNamespace.Interfaces;
using DefaultNamespace.models;
using Messages;
using Microsoft.Extensions.Options;
using WebApp.BLL.Models;
using WebApp.Config;

namespace WebApp.BLL.Services;

public class OrderService(
    UnitOfWork unitOfWork, IOrderRepository orderRepository, IOrderItemRepository orderItemRepository,
    RabbitMqService rabbitMqService, IOptions<RabbitMqSettings> rabbitMqSettings
)
{
    public async Task<OrderUnit[]> BatchInsert(OrderUnit[] orderUnits, CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await unitOfWork.BeginTransactionAsync(token);

        try
        {
            var ordersDal = orderUnits.Select(o => new V1OrderDal
            {
                CustomerId = o.CustomerId,
                DeliveryAddress = o.DeliveryAddress,
                TotalPriceCents = o.TotalPriceCents,
                TotalPriceCurrency = o.TotalPriceCurrency,
                CreatedAt = now,
                UpdatedAt = now,
                Status = "Created"
            }).ToArray();

            var insertedOrders = await orderRepository.BulkInsert(ordersDal, token);

            var orderItemsDal = new List<V1OrderItemDal>();
            for (int i = 0; i < orderUnits.Length; i++)
            {
                var orderId = insertedOrders[i].Id;
                var orderItems = orderUnits[i].OrderItems ?? Array.Empty<OrderItemUnit>();
                orderItemsDal.AddRange(orderItems.Select(oi => new V1OrderItemDal
                {
                    OrderId = orderId,
                    ProductId = oi.ProductId,
                    Quantity = oi.Quantity,
                    ProductTitle = oi.ProductTitle,
                    ProductUrl = oi.ProductUrl,
                    PriceCents = oi.PriceCents,
                    PriceCurrency = oi.PriceCurrency,
                    CreatedAt = now,
                    UpdatedAt = now
                }));
            }

            V1OrderItemDal[] insertedOrderItems = Array.Empty<V1OrderItemDal>();
            if (orderItemsDal.Any())
            {
                insertedOrderItems = await orderItemRepository.BulkInsert(orderItemsDal.ToArray(), token);
            }

            var orderItemLookup = insertedOrderItems.ToLookup(x => x.OrderId);

            await transaction.CommitAsync(token);

            var messages = insertedOrders.Select(order => new OmsOrderCreatedMessage
            {
                Id = order.Id,
                CustomerId = order.CustomerId,
                DeliveryAddress = order.DeliveryAddress,
                TotalPriceCents = order.TotalPriceCents,
                TotalPriceCurrency = order.TotalPriceCurrency,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                OrderItems = orderItemLookup[order.Id]?.Select(item => new OmsOrderCreatedMessage.OrderItemMessage
                {
                    Id = item.Id,
                    OrderId = item.OrderId,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    ProductTitle = item.ProductTitle,
                    ProductUrl = item.ProductUrl,
                    PriceCents = item.PriceCents,
                    PriceCurrency = item.PriceCurrency,
                    CreatedAt = item.CreatedAt,
                    UpdatedAt = item.UpdatedAt
                }).ToArray() ?? Array.Empty<OmsOrderCreatedMessage.OrderItemMessage>()
            }).ToArray();

            await rabbitMqService.Publish(messages, token);

            return Map(insertedOrders, orderItemLookup);
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync(token);
            throw;
        }
    }

    public async Task<OrderUnit[]> GetOrders(QueryOrderItemsModel model, CancellationToken token)
    {
        var orders = await orderRepository.Query(new QueryOrdersDalModel
        {
            Ids = model.Ids,
            CustomerIds = model.CustomerIds,
            Limit = model.PageSize,
            Offset = (model.Page - 1) * model.PageSize
        }, token);

        if (orders.Length is 0)
        {
            return [];
        }

        ILookup<long, V1OrderItemDal> orderItemLookup = null;
        if (model.IncludeOrderItems)
        {
            var orderItems = await orderItemRepository.Query(new QueryOrderItemsDalModel
            {
                OrderIds = orders.Select(x => x.Id).ToArray(),
            }, token);

            orderItemLookup = orderItems.ToLookup(x => x.OrderId);
        }

        return Map(orders, orderItemLookup);
    }

    public async Task UpdateOrdersStatusAsync(long[] orderIds, string newStatus, CancellationToken token)
    {
        if (orderIds.Length == 0) return;

        var orders = await orderRepository.GetByIdsAsync(orderIds.ToList(), token);

        var orderDict = orders.ToDictionary(o => o.Id, o => o);

        foreach (var orderId in orderIds)
        {
            if (!orderDict.TryGetValue(orderId, out var order))
                continue;

            if (!CanTransition(order.Status, newStatus))
                throw new InvalidOperationException($"Cannot transition from '{order.Status}' to '{newStatus}' for order {orderId}.");
        }

        await orderRepository.UpdateStatusesAsync(orderIds.ToList(), newStatus, token);

        var statusChangedMessages = orderIds.Select(id => new OmsOrderStatusChangedMessage
        {
            OrderId = id,
            OrderStatus = newStatus
        }).ToArray();

        await rabbitMqService.Publish(statusChangedMessages, token);
    }

    private bool CanTransition(string currentStatus, string newStatus)
    {
        var transitions = new Dictionary<string, HashSet<string>>
        {
            ["Created"] = new() { "Processed", "Cancelled" },
            ["Processed"] = new() { "Completed", "Cancelled" },
            ["Cancelled"] = new(),
            ["Completed"] = new(),
        };

        return transitions.TryGetValue(currentStatus, out var allowed) && allowed.Contains(newStatus);
    }

    private OrderUnit[] Map(V1OrderDal[] orders, ILookup<long, V1OrderItemDal> orderItemLookup = null)
    {
        return orders.Select(x => new OrderUnit
        {
            Id = x.Id,
            CustomerId = x.CustomerId,
            DeliveryAddress = x.DeliveryAddress,
            TotalPriceCents = x.TotalPriceCents,
            TotalPriceCurrency = x.TotalPriceCurrency,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt,
            OrderItems = orderItemLookup?[x.Id].Select(o => new OrderItemUnit
            {
                Id = o.Id,
                OrderId = o.OrderId,
                ProductId = o.ProductId,
                Quantity = o.Quantity,
                ProductTitle = o.ProductTitle,
                ProductUrl = o.ProductUrl,
                PriceCents = o.PriceCents,
                PriceCurrency = o.PriceCurrency,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt
            }).ToArray() ?? []
        }).ToArray();
    }
}