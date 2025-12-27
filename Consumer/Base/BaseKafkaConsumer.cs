using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Common;
using Consumer.Config;

namespace Consumer.Base
{
    public abstract class BaseKafkaConsumer<T> : IHostedService
        where T : class
    {
        private readonly IConsumer<string, string> _consumer;
        private readonly ILogger<BaseKafkaConsumer<T>> _logger;
        private readonly string _topic;

        protected BaseKafkaConsumer(
            IOptions<KafkaSettings> kafkaSettings,
            string topic,
            ILogger<BaseKafkaConsumer<T>> logger)
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = kafkaSettings.Value.BootstrapServers,
                GroupId = kafkaSettings.Value.GroupId,
                AutoOffsetReset = AutoOffsetReset.Latest,
                EnableAutoCommit = true,
                AutoCommitIntervalMs = 5_000,
                SessionTimeoutMs = 60_000,
                HeartbeatIntervalMs = 3_000,
                MaxPollIntervalMs = 300_000
            };

            _logger = logger;
            _topic = topic;
            _consumer = new ConsumerBuilder<string, string>(config).Build();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _ = Task.Run(() => StartConsuming(_topic, cancellationToken), cancellationToken);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopConsuming();
            return Task.CompletedTask;
        }

        private async Task StartConsuming(string topic, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Subscribing to topic: {Topic}", topic);
                _consumer.Subscribe(topic);
                _logger.LogInformation("Successfully subscribed to topic: {Topic}", topic);

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResult = _consumer.Consume(cancellationToken);

                        if (consumeResult?.Message == null)
                        {
                            _logger.LogDebug("Received null message, continuing...");
                            continue;
                        }

                        _logger.LogDebug("Received message from topic {Topic}, partition {Partition}, offset {Offset}, key: {Key}",
                            topic, consumeResult.Partition, consumeResult.Offset, consumeResult.Message.Key);

                        try
                        {
                            var msg = new Message<T>
                            {
                                Key = consumeResult.Message.Key,
                                Body = consumeResult.Message.Value.FromJson<T>()
                            };

                            await ProcessMessage(msg, cancellationToken);
                            _consumer.Commit(consumeResult);
                            _logger.LogDebug("Successfully processed and committed message from topic {Topic}, offset {Offset}",
                                topic, consumeResult.Offset);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, "Error processing message from topic {Topic}, offset {Offset}", 
                                topic, consumeResult.Offset);
                            // Не коммитим сообщение при ошибке, чтобы оно было обработано повторно
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Consume error occurred: {Error}, IsFatal: {IsFatal}", 
                            ex.Error.Reason, ex.Error.IsFatal);
                        
                        if (ex.Error.IsFatal)
                        {
                            _logger.LogError("Fatal consume error, stopping consumer");
                            break;
                        }
                        
                        // Продолжаем работу, чтобы не упасть при временных ошибках
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Consumer cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in consumer: {Message}", ex.Message);
            }
            finally
            {
                StopConsuming();
            }
        }

        private void StopConsuming()
        {
            _logger.LogInformation($"Stopping consuming from topic: {_topic}");
            _consumer.Close();
            _consumer.Dispose();
        }

        protected abstract Task ProcessMessage(Message<T> message, CancellationToken token);
    }
}

