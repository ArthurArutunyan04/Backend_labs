using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Common;
using Consumer.Config;

namespace Consumer.Base
{
    public abstract class BaseBatchKafkaConsumer<T> : IHostedService
        where T : class
    {
        private readonly IConsumer<string, string> _consumer;
        private readonly ILogger<BaseBatchKafkaConsumer<T>> _logger;
        private readonly string _topic;
        private readonly int _collectBatchSize;
        private readonly int _collectTimeoutMs;

        protected BaseBatchKafkaConsumer(
            IOptions<KafkaSettings> kafkaSettings,
            string topic,
            ILogger<BaseBatchKafkaConsumer<T>> logger)
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = kafkaSettings.Value.BootstrapServers,
                GroupId = kafkaSettings.Value.GroupId,
                AutoOffsetReset = AutoOffsetReset.Latest,
                EnableAutoCommit = false, // Отключаем автокоммит, коммитим вручную после обработки батча
                SessionTimeoutMs = 60_000,
                HeartbeatIntervalMs = 3_000,
                MaxPollIntervalMs = 300_000
            };

            _logger = logger;
            _topic = topic;
            _collectBatchSize = kafkaSettings.Value.CollectBatchSize;
            _collectTimeoutMs = kafkaSettings.Value.CollectTimeoutMs;
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
                        var batch = await CollectBatch(cancellationToken);
                        
                        if (batch.Count == 0)
                        {
                            continue;
                        }

                        _logger.LogDebug("Collected batch of {Count} messages from topic {Topic}", batch.Count, topic);

                        try
                        {
                            var messages = batch.Select(consumeResult => new Message<T>
                            {
                                Key = consumeResult.Message.Key,
                                Body = consumeResult.Message.Value.FromJson<T>()
                            }).ToArray();

                            await ProcessBatch(messages, cancellationToken);
                            
                            // Коммитим офсеты всех сообщений из батча
                            var offsets = batch.Select(cr => cr.TopicPartitionOffset).ToList();
                            _consumer.Commit(offsets);
                            _logger.LogDebug("Successfully processed and committed batch of {Count} messages from topic {Topic}",
                                batch.Count, topic);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, "Error processing batch from topic {Topic}, batch size: {Count}", 
                                topic, batch.Count);
                            // Не коммитим сообщения при ошибке, чтобы они были обработаны повторно
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

        private async Task<List<ConsumeResult<string, string>>> CollectBatch(CancellationToken cancellationToken)
        {
            var batch = new List<ConsumeResult<string, string>>();
            var timeout = TimeSpan.FromMilliseconds(_collectTimeoutMs);
            var startTime = DateTime.UtcNow;

            while (batch.Count < _collectBatchSize && !cancellationToken.IsCancellationRequested)
            {
                var elapsed = DateTime.UtcNow - startTime;
                var remainingTime = timeout - elapsed;
                
                // Если таймаут истек и есть сообщения - возвращаем батч
                if (remainingTime <= TimeSpan.Zero && batch.Count > 0)
                {
                    break;
                }

                // Если таймаут истек и батч пуст - возвращаем пустой батч
                if (remainingTime <= TimeSpan.Zero && batch.Count == 0)
                {
                    break;
                }

                try
                {
                    // Используем оставшееся время или минимальный таймаут
                    var consumeTimeout = remainingTime > TimeSpan.Zero 
                        ? remainingTime 
                        : TimeSpan.FromMilliseconds(100);
                    
                    var consumeResult = _consumer.Consume(consumeTimeout);
                    
                    if (consumeResult?.Message == null)
                    {
                        // Если нет сообщений и батч пуст, продолжаем ожидание
                        if (batch.Count == 0)
                        {
                            continue;
                        }
                        // Если батч не пуст, возвращаем его
                        break;
                    }

                    batch.Add(consumeResult);
                }
                catch (ConsumeException ex)
                {
                    // В Confluent.Kafka таймаут обычно возвращает null, а не исключение
                    // Если возникло исключение, это реальная ошибка - пробрасываем её
                    // Если батч не пуст, возвращаем его перед пробросом
                    if (batch.Count > 0)
                    {
                        break;
                    }
                    throw;
                }
            }

            return batch;
        }

        private void StopConsuming()
        {
            _logger.LogInformation($"Stopping consuming from topic: {_topic}");
            _consumer.Close();
            _consumer.Dispose();
        }

        protected abstract Task ProcessBatch(Message<T>[] messages, CancellationToken token);
    }
}

