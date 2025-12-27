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
    public class OmsOrderCreatedConsumer(
        IOptions<KafkaSettings> kafkaSettings,
        IServiceProvider serviceProvider,
        ILogger<OmsOrderCreatedConsumer> logger)
        : BaseKafkaConsumer<OmsOrderCreatedMessage>(
            kafkaSettings,
            kafkaSettings.Value.OmsOrderCreatedTopic,
            logger)
    {
        protected override async Task ProcessMessage(Message<OmsOrderCreatedMessage> message, CancellationToken token)
        {
            using var scope = serviceProvider.CreateScope();
                    var client = scope.ServiceProvider.GetRequiredService<OmsClient>();

            // Запрос на ручку api/v1/audit/log-order
                    await client.LogOrder(new V1AuditLogOrderRequest
                    {
                Orders = message.Body.OrderItems.Select(ol =>
                            new V1AuditLogOrderRequest.LogOrder
                            {
                        OrderId = message.Body.Id,
                        OrderItemId = ol.Id,
                        CustomerId = message.Body.CustomerId,
                                OrderStatus = nameof(OrderStatus.Created)
                            }).ToArray()
            }, token);
        }
    }
}
