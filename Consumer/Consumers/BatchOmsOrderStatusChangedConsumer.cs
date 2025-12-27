using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Consumer.Base;
using Consumer.Clients;
using Consumer.Config;
using Models.Dto.V1.Requests;
using Messages;

namespace Consumer.Consumers
{
    public class BatchOmsOrderStatusChangedConsumer(
        IOptions<KafkaSettings> kafkaSettings,
        IServiceProvider serviceProvider,
        ILogger<BatchOmsOrderStatusChangedConsumer> logger)
        : BaseBatchKafkaConsumer<OmsOrderStatusChangedMessage>(
            kafkaSettings,
            kafkaSettings.Value.OmsOrderStatusChangedTopic,
            logger)
    {
        protected override async Task ProcessBatch(Message<OmsOrderStatusChangedMessage>[] messages, CancellationToken token)
        {
            using var scope = serviceProvider.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<OmsClient>();

            // Группируем все OrderItemIds из всех сообщений батча в один запрос
            var allOrders = messages
                .SelectMany(msg => msg.Body.OrderItemIds.Select(orderItemId =>
                    new V1AuditLogOrderRequest.LogOrder
                    {
                        OrderId = msg.Body.OrderId,
                        OrderItemId = orderItemId,
                        CustomerId = msg.Body.CustomerId,
                        OrderStatus = msg.Body.OrderStatus
                    }))
                .ToArray();

            // Отправляем один запрос на ручку api/v1/audit/log-order для всего батча
            await client.LogOrder(new V1AuditLogOrderRequest
            {
                Orders = allOrders
            }, token);

            logger.LogInformation("Processed batch of {Count} messages, {OrderCount} orders", 
                messages.Length, allOrders.Length);
        }
    }
}

