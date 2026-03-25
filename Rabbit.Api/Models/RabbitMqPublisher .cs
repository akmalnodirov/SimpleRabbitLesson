using Microsoft.Extensions.Options;
using Rabbit.Contracts;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Rabbit.Api.Models;

public sealed class RabbitMqPublisher : IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly RabbitMqOptions _options;

    public RabbitMqPublisher(
        IConnectionFactory connectionFactory,
        IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;

        _connection = connectionFactory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

        _channel.ExchangeDeclareAsync(
            exchange: _options.ExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false).GetAwaiter().GetResult();

        _channel.QueueDeclareAsync(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false).GetAwaiter().GetResult();

        _channel.QueueBindAsync(
            queue: _options.QueueName,
            exchange: _options.ExchangeName,
            routingKey: _options.RoutingKey).GetAwaiter().GetResult();
    }

    public async Task PublishAsync(CreateJobMessage message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        await _channel.BasicPublishAsync(
            exchange: _options.ExchangeName,
            routingKey: _options.RoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.DisposeAsync();
        await _connection.DisposeAsync();
    }
}