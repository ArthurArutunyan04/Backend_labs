using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Consumer.Base;
using Consumer.Clients;
using Consumer.Config;
using Models.Dto.V1.Requests;
using Messages;

namespace Consumer.Consumers
{
    public class OmsOrderStatusChangedConsumer(
        IOptions<KafkaSettings> kafkaSettings,
        IServiceProvider serviceProvider,
        ILogger<OmsOrderStatusChangedConsumer> logger)
        : BaseKafkaConsumer<OmsOrderStatusChangedMessage>(
            kafkaSettings,
            kafkaSettings.Value.OmsOrderStatusChangedTopic,
            logger)
    {
        protected override async Task ProcessMessage(Message<OmsOrderStatusChangedMessage> message, CancellationToken token)
        {
            using var scope = serviceProvider.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<OmsClient>();

            // Запрос на ручку api/v1/audit/log-order
            await client.LogOrder(new V1AuditLogOrderRequest
            {
                Orders = message.Body.OrderItemIds.Select(orderItemId =>
                    new V1AuditLogOrderRequest.LogOrder
                    {
                        OrderId = message.Body.OrderId,
                        OrderItemId = orderItemId,
                        CustomerId = message.Body.CustomerId,
                        OrderStatus = message.Body.OrderStatus
                    }).ToArray()
            }, token);
        }
    }
}

