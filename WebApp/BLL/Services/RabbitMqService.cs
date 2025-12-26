using System.Text;
using Common;
using Messages;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using WebApp.Config;

namespace WebApp.BLL.Services;

public class RabbitMqService(IOptions<RabbitMqSettings> settings) : IDisposable
{
    private readonly ConnectionFactory _factory = new()
    {
        HostName = settings.Value.HostName,
        Port = settings.Value.Port
    };

    private IConnection _connection;
    private IChannel _channel;

    private async Task<IChannel> Configure(CancellationToken token)
    {
        if (_channel is not null)
        {
            return _channel;
        }

        _connection ??= await _factory.CreateConnectionAsync(token);

        _channel = await _connection.CreateChannelAsync(cancellationToken: token);
        await _channel.ExchangeDeclareAsync(settings.Value.Exchange, ExchangeType.Topic, cancellationToken: token);

        foreach (var mapping in settings.Value.ExchangeMappings)
        {
            var args = mapping.DeadLetter is null ? null : new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", mapping.DeadLetter.Dlx },
                { "x-dead-letter-routing-key", mapping.DeadLetter.RoutingKey }
            };

            await _channel.QueueDeclareAsync(
                queue: mapping.Queue,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: args,
                cancellationToken: token);

            await _channel.QueueBindAsync(
                queue: mapping.Queue,
                exchange: settings.Value.Exchange,
                routingKey: mapping.RoutingKeyPattern,
                cancellationToken: token);
        }

        return _channel;
    }

    public async Task Publish<T>(IEnumerable<T> enumerable, CancellationToken token)
        where T : BaseMessage
    {
        var channel = await Configure(token);

        foreach (var message in enumerable)
        {
            var messageStr = message.ToJson();
            var body = Encoding.UTF8.GetBytes(messageStr);
            await channel.BasicPublishAsync(
                exchange: settings.Value.Exchange,
                routingKey: message.RoutingKey,
                body: body,
                cancellationToken: token);
        }
    }

    public void Dispose()
    {
        DisposeConnection();
        GC.SuppressFinalize(this);
    }

    ~RabbitMqService()
    {
        DisposeConnection();
    }

    private void DisposeConnection()
    {
        _channel?.Dispose();
        _channel = null;
        _connection?.Dispose();
        _connection = null;
    }
}