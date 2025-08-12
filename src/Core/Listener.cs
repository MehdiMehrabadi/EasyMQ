using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EasyMQ.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    private readonly uint _retryCount;
    private readonly ILogger<Listener<T>> _logger;

    public Listener(
        MessagePublisher messagePublisher,
        MessageManagerSettings messageManagerSettings,
        QueueSettings settings,
        IServiceScopeFactory serviceScopeFactory, ILogger<Listener<T>> logger)
    {
        _messagePublisher = messagePublisher;
        _messageManagerSettings = messageManagerSettings;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;

        var queue = settings.Queues.First(q => q.Type == typeof(T));
        _queueName = queue.Name;
        _prefetchCount = queue.prefetchCount;
        _retryCount = queue.retryCount;
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

            T? response = null;

            try
            {
                response = JsonSerializer.Deserialize<T>(
                    message.Body.Span,
                    _messageManagerSettings.JsonSerializerOptions ?? JsonOptions.Default
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deserialization error");
                _messagePublisher.AckMessage(message);
                return;
            }

            try
            {
                await receiver.ReceiveAsync(response!, stoppingToken);
                _messagePublisher.AckMessage(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in ReceiveAsync");
                var retryCount = GetRetryCountFromHeaders(message.BasicProperties.Headers);

                if (retryCount < _retryCount)
                {
                    _messagePublisher.RepublishToErrorExchange(
                        body: message.Body,
                        routingKey: _queueName,
                        originalProperties: message.BasicProperties,
                        nextRetryCount: retryCount + 1
                    );
                    _messagePublisher.AckMessage(message);
                }
                else
                {
                    try
                    {
                        await receiver.HandleErrorAsync(response!, stoppingToken);
                        _messagePublisher.AckMessage(message);
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception, "Exception in HandleErrorAsync");
                        _messagePublisher.NackMessage(message);
                    }
                }
            }

            stoppingToken.ThrowIfCancellationRequested();
        };

        _messagePublisher.Channel.BasicConsume(_queueName, autoAck: false, consumer);
        return Task.CompletedTask;
    }

    private int GetRetryCountFromHeaders(IDictionary<string, object> headers)
    {
        if (headers == null || !headers.TryGetValue("retry-count", out var value)) return 0;
        try
        {
            return value switch
            {
                byte[] bytes => int.Parse(Encoding.UTF8.GetString(bytes)),
                int i => i,
                long l => (int)l,
                _ => 0
            };
        }
        catch
        {
            return 0;
        }
    }

    public override void Dispose()
    {
        _messagePublisher.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}

