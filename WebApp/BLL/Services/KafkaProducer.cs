using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Common;
using WebApp.Config;

namespace WebApp.BLL.Services
{
    public class KafkaProducer
    {
        private readonly IProducer<string, string> _producer;
        private readonly ILogger<KafkaProducer> _logger;

        public KafkaProducer(IOptions<KafkaSettings> kafkaSettings, ILogger<KafkaProducer> logger)
        {
            _logger = logger;
            var config = new ProducerConfig
            {
                BootstrapServers = kafkaSettings.Value.BootstrapServers,
                ClientId = kafkaSettings.Value.ClientId,
                LingerMs = 100,
                CompressionType = CompressionType.Snappy,
                Partitioner = Partitioner.Consistent
            };

            _producer = new ProducerBuilder<string, string>(config)
                .SetErrorHandler((_, e) => logger.LogError("Producer error: {Reason}", e.Reason))
                .Build();
        }

        public async Task Produce<T>(string topic, (string key, T message)[] messages, CancellationToken token)
        {
            _logger.LogInformation("Producing {Count} messages to topic {Topic}", messages.Length, topic);
            
            var tasks = messages.Select(async message =>
            {
                try
                {
                    var kafkaMessage = new Message<string, string>
                    {
                        Key = message.key,
                        Value = message.message.ToJson()
                    };
                    
                    DeliveryResult<string, string> result;
                    
                    // Вычисляем партицию из ключа: если ключ - число, то партиция = ключ % 5
                    // Это позволяет использовать CustomerId как ключ (например, 90, 79), 
                    // но распределять по партициям 0-4 на основе остатка от деления
                    if (long.TryParse(message.key, out var customerId))
                    {
                        var partition = (int)(customerId % 5);
                        var topicPartition = new TopicPartition(topic, partition);
                        result = await _producer.ProduceAsync(topicPartition, kafkaMessage, token);
                    }
                    else
                    {
                        // Если ключ не число, используем автоматическое распределение
                        result = await _producer.ProduceAsync(topic, kafkaMessage, token);
                    }
                    
                    _logger.LogDebug("Message produced to topic {Topic}, partition {Partition}, offset {Offset}, key: {Key}", 
                        topic, result.Partition, result.Offset, message.key);
                    return result;
                }
                catch (ProduceException<string, string> ex)
                {
                    _logger.LogError(ex, "Failed to send message to topic {Topic}: {Reason}", topic, ex.Error.Reason);
                    return null;
                }
            });

            var results = await Task.WhenAll(tasks);

            var failedCount = results.Count(x => x is null);
            if (failedCount > 0)
            {
                _logger.LogError("Failed to produce {Count} out of {Total} messages to topic {Topic}", 
                    failedCount, messages.Length, topic);
                throw new Exception($"Failed to produce {failedCount} messages to topic {topic}");
            }
            
            _logger.LogInformation("Successfully produced {Count} messages to topic {Topic}", messages.Length, topic);
        }
    }
}

