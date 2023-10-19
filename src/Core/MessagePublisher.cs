using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EasyMQ.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EasyMQ.Core;

internal class MessagePublisher : IMessagePublisher, IDisposable
{
    private const string MaxPriorityHeader = "x-max-priority";
    private const string DeadLetterExchange = "x-dead-letter-exchange";
    private const string MessageTtl = "x-message-ttl";
    internal IConnection Connection { get; private set; }
    internal IModel Channel { get; private set; }
    private readonly MessageManagerSettings _messageManagerSettings;
    private readonly QueueSettings _queueSettings;
    public MessagePublisher(MessageManagerSettings messageManagerSettings, QueueSettings queueSettings)
    {

        var exchangeError = $"{messageManagerSettings.ExchangeName}_error";

        var factory = new ConnectionFactory
        {
            HostName = messageManagerSettings.Host,
            Port = messageManagerSettings.Port,
            UserName = messageManagerSettings.UserName,
            Password = messageManagerSettings.Password,
            VirtualHost = messageManagerSettings.VirtualHost
        };
        Connection = factory.CreateConnection();

        Channel = Connection.CreateModel();


        Channel.ExchangeDeclare(messageManagerSettings.ExchangeName,
            type: ExchangeType.Direct, true);

        Channel.ExchangeDeclare(exchangeError,
            type: ExchangeType.Direct, true);


        foreach (var queue in queueSettings.Queues)
        {
            var queueError = $"{queue.Name}_error";
            var args = new Dictionary<string, object>
            {
                [MaxPriorityHeader] = 10,
                [DeadLetterExchange] = exchangeError,
            };
            Channel.QueueDeclare(queue.Name, durable: true, exclusive: false, autoDelete: false, args);
            Channel.QueueBind(queue.Name, messageManagerSettings.ExchangeName, queue.Name);

          
            var errorArgs = new Dictionary<string, object>
            {
                [MaxPriorityHeader] = 10,
                [DeadLetterExchange] = messageManagerSettings.ExchangeName,
                [MessageTtl] = 10000,
            };
            Channel.QueueDeclare(queueError, durable: true, exclusive: false, autoDelete: false, arguments: errorArgs);
            Channel.QueueBind(queueError, exchangeError, queue.Name, errorArgs);

        }

        this._messageManagerSettings = messageManagerSettings;
        this._queueSettings = queueSettings;
    }

    public Task PublishAsync<T>(T message, int priority = 1, TimeSpan? keepAliveTime = null) where T : class
    {
        var sendBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize<object>(message, _messageManagerSettings.JsonSerializerOptions ?? JsonOptions.Default));
        var routingKey = _queueSettings.Queues.First(q => q.Type == typeof(T)).Name;
        var properties = Channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.Priority = Convert.ToByte(priority);
        properties.Expiration = keepAliveTime?.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
        properties.MessageId = Guid.NewGuid().ToString("N");
        Channel.BasicPublish(_messageManagerSettings.ExchangeName, routingKey, properties, sendBytes.AsMemory());
        return Task.CompletedTask;
    }

    public void AckMessage(BasicDeliverEventArgs message) => Channel.BasicAck(message.DeliveryTag, false);

    public void NackMessage(BasicDeliverEventArgs message) => Channel.BasicNack(message.DeliveryTag, false, false);

    public void Dispose()
    {
        try
        {
            if (Channel.IsOpen)
            {
                Channel.Close();
            }

            if (Connection.IsOpen)
            {
                Connection.Close();
            }
        }
        catch
        {
        }

        GC.SuppressFinalize(this);
    }
}
