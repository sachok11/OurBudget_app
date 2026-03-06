using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace FamilyBudget.Infrastructure.Services;

public interface IMessageQueueService
{
    Task PublishAsync<T>(string queueName, T message);
    void Subscribe<T>(string queueName, Func<T, Task> handler);
}

public class RabbitMQService : IMessageQueueService, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;

    public RabbitMQService(IConfiguration configuration)
    {
        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
            UserName = configuration["RabbitMQ:UserName"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest",
            DispatchConsumersAsync = true,
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
    }

    public async Task PublishAsync<T>(string queueName, T message)
    {
        _channel.QueueDeclare(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: queueName,
            basicProperties: properties,
            body: body);

        await Task.CompletedTask;
    }

    public void Subscribe<T>(string queueName, Func<T, Task> handler)
    {
        _channel.QueueDeclare(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var consumer = new EventingBasicConsumer(_channel);
        
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            var message = JsonSerializer.Deserialize<T>(json);

            if (message != null)
            {
                await handler(message);
                _channel.BasicAck(ea.DeliveryTag, false);
            }
        };

        _channel.BasicConsume(
            queue: queueName,
            autoAck: false,
            consumer: consumer);
    }

    public void Dispose()
    {
    _channel?.Close();
    _connection?.Close();
    }
}