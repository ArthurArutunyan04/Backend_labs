using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Common;
using Messages;
using Consumer.Base;
using Consumer.Clients;
using Consumer.Config;
using Models.Dto.V1.Requests;

namespace Consumer.Consumers
{
    public class BatchOmsOrderCreatedConsumer(
        IOptions<KafkaSettings> kafkaSettings,
        IServiceProvider serviceProvider,
        ILogger<BatchOmsOrderCreatedConsumer> logger)
        : BaseBatchKafkaConsumer<OmsOrderCreatedMessage>(
            kafkaSettings,
            kafkaSettings.Value.OmsOrderCreatedTopic,
            logger)
    {
        protected override async Task ProcessBatch(Message<OmsOrderCreatedMessage>[] messages, CancellationToken token)
        {
            using var scope = serviceProvider.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<OmsClient>();

            // Группируем все OrderItems из всех сообщений батча в один запрос
            var allOrders = messages
                .SelectMany(msg => msg.Body.OrderItems.Select(ol =>
                    new V1AuditLogOrderRequest.LogOrder
                    {
                        OrderId = msg.Body.Id,
                        OrderItemId = ol.Id,
                        CustomerId = msg.Body.CustomerId,
                        OrderStatus = nameof(OrderStatus.Created)
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

