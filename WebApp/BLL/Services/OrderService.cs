using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderStatusEnum = Common.OrderStatus;
using WebApp.BLL.Models;
using WebApp.Config;
using WebApp.DAL;
using WebApp.DAL.Interfaces;
using WebApp.DAL.Models;
using Messages;

namespace WebApp.BLL.Services;
public class OrderService(UnitOfWork unitOfWork, IOrderRepository orderRepository, IOrderItemRepository orderItemRepository,
    KafkaProducer kafkaProducer, IOptions<KafkaSettings> kafkaSettings, ILogger<OrderService> logger)
{
    /// <summary>
    /// Метод создания заказов
    /// </summary>
    /// 
    public async Task<OrderUnit[]> BatchInsert(OrderUnit[] orderUnits, CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await unitOfWork.BeginTransactionAsync(token);

        try
        {
            // 1. Подготавливаем данные для вставки в orders
            var orderDals = orderUnits.Select(order => new V1OrderDal
            {
                CustomerId = order.CustomerId,
                DeliveryAddress = order.DeliveryAddress,
                TotalPriceCents = order.TotalPriceCents,
                TotalPriceCurrency = order.TotalPriceCurrency,
                OrderStatus = string.IsNullOrWhiteSpace(order.OrderStatus) || order.OrderStatus.StartsWith("OrderStatus")
                    ? nameof(OrderStatusEnum.Created)
                    : order.OrderStatus,
                CreatedAt = now,
                UpdatedAt = now
            }).ToArray();

            // 2. Вставляем заказы и получаем их с ID
            var insertedOrders = await orderRepository.BulkInsert(orderDals, token);

            // 3. Подготавливаем данные для order_items
            var orderItemDals = new List<V1OrderItemDal>();

            // Сопоставляем временные order_id с реальными ID из базы
            for (int i = 0; i < orderUnits.Length; i++)
            {
                var orderUnit = orderUnits[i];
                var insertedOrder = insertedOrders[i];

                if (orderUnit.OrderItems?.Length > 0)
                {
                    var itemsForOrder = orderUnit.OrderItems.Select(item => new V1OrderItemDal
                    {
                        OrderId = insertedOrder.Id, // Используем реальный ID заказа
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        ProductTitle = item.ProductTitle,
                        ProductUrl = item.ProductUrl,
                        PriceCents = item.PriceCents,
                        PriceCurrency = item.PriceCurrency,
                        CreatedAt = now,
                        UpdatedAt = now
                    });

                    orderItemDals.AddRange(itemsForOrder);
                }
            }

            // 4. Вставляем позиции заказов
            V1OrderItemDal[] insertedOrderItems = [];
            if (orderItemDals.Count > 0)
            {
                insertedOrderItems = await orderItemRepository.BulkInsert(orderItemDals.ToArray(), token);
            }

            // 5. Собираем результат
            var result = Map(insertedOrders, insertedOrderItems.ToLookup(x => x.OrderId));

            logger.LogInformation("[OrderService] Committing transaction for {Count} orders", orderUnits.Length);
            await transaction.CommitAsync(token);
            logger.LogInformation("[OrderService] Transaction committed successfully");

            // Публикуем события в Kafka после успешного коммита
            try
            {
                var messages = result.Select(order => new OmsOrderCreatedMessage
                {
                    Id = order.Id,
                    CustomerId = order.CustomerId,
                    DeliveryAddress = order.DeliveryAddress,
                    TotalPriceCents = order.TotalPriceCents,
                    TotalPriceCurrency = order.TotalPriceCurrency,
                    CreatedAt = order.CreatedAt,
                    UpdatedAt = order.UpdatedAt,
                    OrderItems = order.OrderItems.Select(item => new OmsOrderItemMessage
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
                    }).ToArray()
                }).ToArray();

                // Взят за ключ CustomerId, чтобы события по заказам одного и того же пользователя попадали в одну партицию
                // Это нужно, чтобы не потерять очередность, и чтобы один и тот же консьюмер читал события по этому пользователю.
                var kafkaMessages = messages.Select(msg => (key: msg.CustomerId.ToString(), message: msg)).ToArray();

                logger.LogInformation("[OrderService] Publishing {Count} messages to Kafka", messages.Length);
                await kafkaProducer.Produce(kafkaSettings.Value.OmsOrderCreatedTopic, kafkaMessages, token);
                logger.LogInformation("[OrderService] Successfully published messages to Kafka");
            }
            catch (Exception ex)
            {
                // Логируем ошибку публикации, но не откатываем транзакцию, так как она уже закоммичена
                logger.LogError(ex, "[OrderService] Error publishing messages to Kafka. Orders are already saved in DB.");
                // Не пробрасываем исключение, чтобы не потерять данные в БД
            }

            return result;
        }
        catch (Exception e)
        {
            logger.LogError(e, "[OrderService] Error in BatchInsert, rolling back transaction");
            await transaction.RollbackAsync(token);
            throw;
        }
    }

    /// <summary>
    /// Метод получения заказов
    /// </summary>
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

    public async Task UpdateOrderStatuses(long[] orderIds, string newStatus, CancellationToken token)
    {
        if (orderIds is null or { Length: 0 })
        {
            return;
        }

        var targetStatus = ParseStatus(newStatus);

        await using var transaction = await unitOfWork.BeginTransactionAsync(token);
        try
        {
            var existingOrders = await orderRepository.Query(new QueryOrdersDalModel
            {
                Ids = orderIds
            }, token);

            if (existingOrders.Length is 0)
            {
                await transaction.CommitAsync(token);
                return;
            }

            foreach (var order in existingOrders)
            {
                var currentStatus = ParseStatus(order.OrderStatus);
                if (currentStatus == targetStatus)
                {
                    continue;
                }

                if (!CanTransition(currentStatus, targetStatus))
                {
                    throw new InvalidOperationException(
                        $"Order {order.Id} cannot move from {currentStatus} to {targetStatus}");
                }
            }

            var idsToUpdate = existingOrders.Select(o => o.Id).ToArray();
            await orderRepository.UpdateStatuses(idsToUpdate, targetStatus.ToString(), token);
            await transaction.CommitAsync(token);

            // Публикуем события в Kafka после успешного коммита
            try
            {
                var orderIdsToPublish = existingOrders
                    .Where(o => ParseStatus(o.OrderStatus) != targetStatus)
                    .Select(o => o.Id)
                    .ToArray();

                if (orderIdsToPublish.Length > 0)
                {
                    // Получаем позиции заказов для аудит-лога
                    var orderItems = await orderItemRepository.Query(new QueryOrderItemsDalModel
                    {
                        OrderIds = orderIdsToPublish
                    }, token);

                    var orderItemsLookup = orderItems.ToLookup(x => x.OrderId);

                    var statusChangedMessages = existingOrders
                        .Where(o => ParseStatus(o.OrderStatus) != targetStatus)
                        .Select(order => new OmsOrderStatusChangedMessage
                        {
                            OrderId = order.Id,
                            CustomerId = order.CustomerId,
                            OrderStatus = targetStatus.ToString(),
                            OrderItemIds = orderItemsLookup[order.Id].Select(oi => oi.Id).ToArray()
                        }).ToArray();


                    // Взят за ключ CustomerId, чтобы события по заказам одного и того же пользователя попадали в одну партицию
                    // Это нужно, чтобы не потерять очередность, и чтобы один и тот же консьюмер читал события по этому пользователю.
                    var kafkaMessages = statusChangedMessages.Select(msg => (key: msg.CustomerId.ToString(), message: msg)).ToArray();

                    logger.LogInformation("[OrderService] Publishing {Count} status change messages to Kafka", statusChangedMessages.Length);
                    await kafkaProducer.Produce(kafkaSettings.Value.OmsOrderStatusChangedTopic, kafkaMessages, token);
                    logger.LogInformation("[OrderService] Successfully published status change messages to Kafka");
                }
            }
            catch (Exception ex)
            {
                // Логируем ошибку публикации, но не откатываем транзакцию, так как она уже закоммичена
                logger.LogError(ex, "[OrderService] Error publishing status change messages to Kafka. Orders are already updated in DB.");
                // Не пробрасываем исключение, чтобы не потерять данные в БД
            }
        }
        catch
        {
            await transaction.RollbackAsync(token);
            throw;
        }
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
            OrderStatus = x.OrderStatus,
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

    private static OrderStatusEnum ParseStatus(string status)
    {
        if (Enum.TryParse<OrderStatusEnum>(status, true, out var parsed))
        {
            return parsed;
        }

        throw new ArgumentException($"Unknown status '{status}'", nameof(status));
    }

    private static bool CanTransition(OrderStatusEnum current, OrderStatusEnum target)
    {
        if (current == target)
        {
            return true;
        }

        return current switch
        {
            OrderStatusEnum.Created => target is OrderStatusEnum.Processing or OrderStatusEnum.Cancelled,
            OrderStatusEnum.Processing => target is OrderStatusEnum.Completed or OrderStatusEnum.Cancelled,
            OrderStatusEnum.Completed => false,
            OrderStatusEnum.Cancelled => false,
            _ => false
        };
    }
}
