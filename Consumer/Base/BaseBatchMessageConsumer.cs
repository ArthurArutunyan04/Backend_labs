using System.Text;
using Common;
using Consumer.Config;
using Messages;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Consumer.Base;

public abstract class BaseBatchMessageConsumer<TMessage> : BackgroundService
    where TMessage : BaseMessage
{
    private readonly RabbitMqSettings _settings;
    private readonly Func<RabbitMqSettings, RabbitMqSettings.TopicSettingsUnit> _getTopicSettings;

    private ConnectionFactory _factory = null!;
    private IConnection _connection = null!;
    private IChannel _channel = null!;

    private readonly List<MessageInfo<TMessage>> _batch = [];
    private Timer? _timer;

    protected BaseBatchMessageConsumer(
        RabbitMqSettings settings,
        Func<RabbitMqSettings, RabbitMqSettings.TopicSettingsUnit> getTopicSettings)
    {
        _settings = settings;
        _getTopicSettings = getTopicSettings;
    }

    protected abstract Task ProcessMessages(TMessage[] messages);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var topicSettings = _getTopicSettings(_settings);

        _factory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            Port = _settings.Port
        };

        _connection = await _factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.ExchangeDeclareAsync(
            exchange: topicSettings.DeadLetter.Dlx,
            type: ExchangeType.Direct,
            durable: true,
            cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(
            queue: topicSettings.DeadLetter.Dlq,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(
            queue: topicSettings.DeadLetter.Dlq,
            exchange: topicSettings.DeadLetter.Dlx,
            routingKey: topicSettings.DeadLetter.RoutingKey,
            cancellationToken: stoppingToken);

        var queueArgs = new Dictionary<string, object>
        {
            {"x-dead-letter-exchange", topicSettings.DeadLetter.Dlx},
            {"x-dead-letter-routing-key", topicSettings.DeadLetter.RoutingKey}
        };

        await _channel.QueueDeclareAsync(
            queue: topicSettings.Queue,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: queueArgs,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            var message = json.FromJson<TMessage>();

            lock (_batch)
            {
                _batch.Add(new MessageInfo<TMessage> { Message = message, DeliveryTag = ea.DeliveryTag });
            }

            if (_batch.Count >= topicSettings.BatchSize)
            {
                await ProcessBatch();
            }
            else
            {
                _timer?.Dispose();
                _timer = new Timer(async _ => await ProcessBatch(), null, TimeSpan.FromSeconds(topicSettings.BatchTimeoutSeconds), Timeout.InfiniteTimeSpan);
            }

            async Task ProcessBatch()
            {
                _timer?.Dispose();
                _timer = null;

                MessageInfo<TMessage>[] batchCopy;
                lock (_batch)
                {
                    batchCopy = _batch.ToArray();
                    _batch.Clear();
                }

                if (batchCopy.Length > 0)
                {
                    try
                    {
                        await ProcessMessages(batchCopy.Select(m => m.Message).ToArray());

                        await _channel.BasicAckAsync(batchCopy[^1].DeliveryTag, true);
                    }
                    catch (Exception)
                    {
                        await _channel.BasicNackAsync(batchCopy[^1].DeliveryTag, multiple: true, requeue: false);
                    }
                }
            }
        };

        await _channel.BasicConsumeAsync(queue: topicSettings.Queue, autoAck: false, consumer: consumer);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        await _channel.CloseAsync(cancellationToken: cancellationToken);
        await _connection.CloseAsync(cancellationToken: cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}