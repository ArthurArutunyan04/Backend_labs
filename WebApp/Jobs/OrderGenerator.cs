using AutoFixture;
using AutoFixture.Kernel;
using Microsoft.Extensions.Logging;
using Common;
using WebApp.BLL.Models;
using WebApp.BLL.Services;

namespace WebApp.Jobs
{
    public class OrderGenerator(IServiceProvider serviceProvider, ILogger<OrderGenerator> logger) : BackgroundService
    {
        private static readonly Random _random = new();
        // Из Created можно перейти только в Processing или Cancelled
        private static readonly OrderStatus[] _firstStatuses = { OrderStatus.Processing, OrderStatus.Cancelled };
        // Из Processing можно перейти в Completed или Cancelled
        private static readonly OrderStatus[] _secondStatuses = { OrderStatus.Completed, OrderStatus.Cancelled };

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("OrderGenerator started");
            var fixture = new Fixture();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var orderService = scope.ServiceProvider.GetRequiredService<OrderService>();

                    logger.LogInformation("[OrderGenerator] Generating 50 orders");
                    var orders = Enumerable.Range(1, 50)
                        .Select(_ =>
                        {
                            var orderItem = fixture.Build<OrderItemUnit>()
                                .With(x => x.PriceCurrency, "RUB")
                                .With(x => x.PriceCents, 1000)
                                .Create();

                            // Генерируем CustomerId любым способом (AutoFixture сгенерирует случайное значение)
                            // При публикации в Kafka ключ будет формироваться как CustomerId % 5,
                            // что обеспечит распределение по 5 партициям (0-4), но сам CustomerId останется неизменным
                            // Например, CustomerId = 90 попадет в партицию 0 (90 % 5 = 0), CustomerId = 7 попадет в партицию 2 (7 % 5 = 2)
                            
                            var order = fixture.Build<OrderUnit>()
                                .With(x => x.TotalPriceCurrency, "RUB")
                                .With(x => x.TotalPriceCents, 1000)
                                .With(x => x.OrderItems, [orderItem])
                                // CustomerId будет сгенерирован AutoFixture автоматически (любое значение)
                                .Without(x => x.Id) // Не генерируем Id, он будет установлен БД
                                .Without(x => x.OrderStatus) // Не генерируем статус, установим вручную
                                .Create();
                            
                            // Явно устанавливаем статус "Created" при создании заказа
                            order.OrderStatus = nameof(OrderStatus.Created);

                            return order;
                        })
                        .ToArray();

                    logger.LogInformation("[OrderGenerator] Calling BatchInsert for {Count} orders", orders.Length);
                    var result = await orderService.BatchInsert(orders, stoppingToken);
                    logger.LogInformation("[OrderGenerator] Successfully generated and published {Count} orders (IDs: {OrderIds})", 
                        result.Length, string.Join(", ", result.Select(o => o.Id)));

                    // Случайное обновление статусов у части заказов
                    // Сначала обновляем из Created в Processing или Cancelled
                    if (result.Length > 0)
                    {
                        var ordersToUpdate = result
                            .OrderBy(_ => _random.Next())
                            .Take(_random.Next(1, Math.Min(result.Length, 20) + 1))
                            .ToArray();

                        if (ordersToUpdate.Length > 0)
                        {
                            var randomStatus = _firstStatuses[_random.Next(_firstStatuses.Length)];
                            var orderIds = ordersToUpdate.Select(o => o.Id).ToArray();

                            logger.LogInformation("[OrderGenerator] Updating status to {Status} for {Count} orders (IDs: {OrderIds})",
                                randomStatus, orderIds.Length, string.Join(", ", orderIds));

                            try
                            {
                                await orderService.UpdateOrderStatuses(orderIds, randomStatus.ToString(), stoppingToken);
                                logger.LogInformation("[OrderGenerator] Successfully updated status for {Count} orders", orderIds.Length);
                                
                                // Если перешли в Processing, можно перейти в Completed или Cancelled
                                if (randomStatus == OrderStatus.Processing && _random.Next(2) == 0)
                                {
                                    var secondStatus = _secondStatuses[_random.Next(_secondStatuses.Length)];
                                    logger.LogInformation("[OrderGenerator] Updating status to {Status} for {Count} orders (IDs: {OrderIds})",
                                        secondStatus, orderIds.Length, string.Join(", ", orderIds));
                                    
                                    try
                                    {
                                        await orderService.UpdateOrderStatuses(orderIds, secondStatus.ToString(), stoppingToken);
                                        logger.LogInformation("[OrderGenerator] Successfully updated status to {Status} for {Count} orders", 
                                            secondStatus, orderIds.Length);
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.LogWarning(ex, "[OrderGenerator] Failed to update status to {Status}", secondStatus);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex, "[OrderGenerator] Failed to update status for orders, this is expected for some transitions");
                            }
                        }
                    }

                    await Task.Delay(250, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[OrderGenerator] Error generating orders, will retry in 5 seconds");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
            
            logger.LogInformation("OrderGenerator stopped");
        }
    }
}
