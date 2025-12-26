using AutoFixture;
using WebApp.BLL.Models;
using WebApp.BLL.Services;

namespace WebApp.Jobs;

public class OrderGenerator(IServiceProvider serviceProvider) : BackgroundService
{
    private static readonly Random Random = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var fixture = new Fixture();
        using var scope = serviceProvider.CreateScope();
        var orderService = scope.ServiceProvider.GetRequiredService<OrderService>();

        while (!stoppingToken.IsCancellationRequested)
        {
            var orders = Enumerable.Range(1, 50)
                .Select(_ =>
                {
                    var orderItem = fixture.Build<OrderItemUnit>()
                        .With(x => x.PriceCurrency, "RUB")
                        .With(x => x.PriceCents, 1000)
                        .Create();

                    var order = fixture.Build<OrderUnit>()
                        .With(x => x.TotalPriceCurrency, "RUB")
                        .With(x => x.TotalPriceCents, 1000)
                        .With(x => x.OrderItems, [orderItem])
                        .Create();

                    return order;
                })
                .ToArray();

            var insertedOrders = await orderService.BatchInsert(orders, stoppingToken);

            if (insertedOrders.Length > 0)
            {
                var maxOrdersToUpdate = Random.Next(0, insertedOrders.Length);
                var ordersToUpdate = insertedOrders
                    .Take(maxOrdersToUpdate)
                    .Where(o => o.Id != 0)
                    .Select(o => o.Id)
                    .ToArray();

                if (ordersToUpdate.Length > 0)
                {
                    var newStatus = Random.Next(0, 2) switch
                    {
                        0 => "Processed",
                        1 => "Cancelled",
                        _ => "Processed"
                    };

                    try
                    {
                        await orderService.UpdateOrdersStatusAsync(ordersToUpdate, newStatus, stoppingToken);
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
            }

            await Task.Delay(250, stoppingToken);
        }
    }
}