using Common;
using Consumer.Base;
using Consumer.Config;
using Messages;
using Microsoft.Extensions.Options;
using Models.Dto.V1.Requests;

namespace Consumer.Consumers;

public class BatchOmsOrderStatusChangedConsumer(
    IOptions<RabbitMqSettings> rabbitMqSettings,
    IServiceProvider serviceProvider)
    : BaseBatchMessageConsumer<OmsOrderStatusChangedMessage>(
        rabbitMqSettings.Value,
        settings => settings.OrderStatusChanged)
{
    protected override async Task ProcessMessages(OmsOrderStatusChangedMessage[] messages)
    {
        using var scope = serviceProvider.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<Client>();

        await client.LogOrder(new V1CreateAuditLogRequest
        {
            Orders = messages.Select(order => new V1CreateAuditLogRequest.LogOrder
            {
                OrderId = order.OrderId,
                OrderStatus = order.OrderStatus
            }).ToArray()
        }, CancellationToken.None);
    }
}