using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EasyMQ.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
namespace EasyMQ.Core;

internal class Listener<T> : BackgroundService where T : class
{
    private readonly MessagePublisher _messagePublisher;
    private readonly MessageManagerSettings _messageManagerSettings;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly string _queueName;
    private readonly int _prefetchCount;
    private readonly int _retryCount;
    private readonly TimeSpan _ttl;

    public Listener(MessagePublisher messagePublisher, MessageManagerSettings messageManagerSettings,
        QueueSettings settings, IServiceScopeFactory serviceScopeFactory)
    {
        this._messagePublisher = messagePublisher;
        this._messageManagerSettings = messageManagerSettings;
        this._serviceScopeFactory = serviceScopeFactory;
        var queue = settings.Queues.First(q => q.Type == typeof(T));
        _queueName = queue.Name;
        _prefetchCount = queue.prefetchCount;
        _retryCount = queue.retryCount;
        _ttl = queue.ttl;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();
        _messagePublisher.Channel.BasicQos(0, (ushort)_prefetchCount, false);
        var consumer = new EventingBasicConsumer(_messagePublisher.Channel);
        consumer.Received += async (_, message) =>
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var receiver = scope.ServiceProvider.GetRequiredService<IReceiver<T>>();
            var errorCounter = scope.ServiceProvider.GetRequiredService<IErrorCounter>();
            var response = JsonSerializer.Deserialize<T>(message.Body.Span, _messageManagerSettings.JsonSerializerOptions ?? JsonOptions.Default);
            try
            {
                await receiver.ReceiveAsync(response, stoppingToken);
                _messagePublisher.AckMessage(message);
            }
            catch (Exception)
            {
                if (_retryCount == -1)
                {
                    // unlimited retry!
                    _messagePublisher.NackMessage(message);
                }
                else
                {
                    var tryCount = await errorCounter.GetTryCountAsync(message.BasicProperties.MessageId);
                    if (tryCount < _retryCount)
                    {
                        tryCount++;
                        await errorCounter.UpdateTryCountAsync(message.BasicProperties.MessageId, tryCount, _ttl);
                        _messagePublisher.NackMessage(message);
                    }
                    else
                    {
                        try
                        {
                            await receiver.HandleErrorAsync(response, stoppingToken);
                            await errorCounter.KillCounterAsync(message.BasicProperties.MessageId);
                            _messagePublisher.AckMessage(message);
                        }
                        catch (Exception)
                        {
                            _messagePublisher.NackMessage(message);
                        }
                    }
                }
            }
            stoppingToken.ThrowIfCancellationRequested();
        };

        _messagePublisher.Channel.BasicConsume(_queueName, autoAck: false, consumer);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _messagePublisher.Dispose();
        base.Dispose();

        GC.SuppressFinalize(this);
    }
}
