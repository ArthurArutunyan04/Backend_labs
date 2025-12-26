using Common;
using Consumer.Base;
using Consumer.Config;
using Messages;
using Microsoft.Extensions.Options;
using Models.Dto.V1.Requests;

namespace Consumer.Consumers;

public class BatchOmsOrderCreatedConsumer(
    IOptions<RabbitMqSettings> rabbitMqSettings,
    IServiceProvider serviceProvider)
    : BaseBatchMessageConsumer<OmsOrderCreatedMessage>(rabbitMqSettings.Value, settings => settings.OrderCreated)
{
    private static int _counter = 0;

    protected override async Task ProcessMessages(OmsOrderCreatedMessage[] messages)
    {
        Interlocked.Increment(ref _counter);

        if (_counter % 5 == 0)
        {
            throw new InvalidOperationException($"Simulated error on batch #{_counter}");
        }

        using var scope = serviceProvider.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<Client>();

        await client.LogOrder(new V1CreateAuditLogRequest
        {
            Orders = messages.SelectMany(order => order.OrderItems.Select(ol =>
                new V1CreateAuditLogRequest.LogOrder
                {
                    OrderId = order.Id,
                    OrderItemId = ol.Id,
                    CustomerId = order.CustomerId,
                    OrderStatus = nameof(OrderStatus.Created)
                })).ToArray()
        }, CancellationToken.None);
    }
}