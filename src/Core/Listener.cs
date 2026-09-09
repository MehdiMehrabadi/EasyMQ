#nullable enable
using System;
using System.Collections.Generic;
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

internal sealed class Listener<T> : BackgroundService where T : class
{
    private readonly MessagePublisher _messagePublisher;
    private readonly MessageManagerSettings _messageManagerSettings;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<Listener<T>> _logger;
    private readonly string _queueName;
    private readonly int _prefetchCount;
    private readonly uint _retryCount;

    private IChannel? _channel;
    private string? _consumerTag;

    public Listener(
        MessagePublisher messagePublisher,
        MessageManagerSettings messageManagerSettings,
        QueueSettings settings,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<Listener<T>> logger)
    {
        _messagePublisher = messagePublisher;
        _messageManagerSettings = messageManagerSettings;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;

        var queue = settings.GetRequired<T>();
        _queueName = queue.Name;
        _prefetchCount = queue.PrefetchCount;
        _retryCount = queue.RetryCount;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        _channel = await _messagePublisher.CreateConsumerChannelAsync(stoppingToken).ConfigureAwait(false);

        await _channel
            .BasicQosAsync(prefetchSize: 0, prefetchCount: (ushort)_prefetchCount, global: false, stoppingToken)
            .ConfigureAwait(false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, message) => OnReceivedAsync(message, stoppingToken);

        _consumerTag = await _channel
            .BasicConsumeAsync(_queueName, autoAck: false, consumer, stoppingToken)
            .ConfigureAwait(false);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful shutdown
        }
        finally
        {
            await StopConsumerAsync().ConfigureAwait(false);
        }
    }

    private async Task OnReceivedAsync(BasicDeliverEventArgs message, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var receiver = scope.ServiceProvider.GetRequiredService<IReceiver<T>>();

            T? response;
            try
            {
                response = JsonSerializer.Deserialize<T>(
                    message.Body.Span,
                    _messageManagerSettings.JsonSerializerOptions ?? JsonOptions.Default);

                if (response is null)
                {
                    _logger.LogError("Deserialization returned null for message on queue {QueueName}", _queueName);
                    await AckAsync(message, stoppingToken).ConfigureAwait(false);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deserialization failed for message on queue {QueueName}", _queueName);
                await AckAsync(message, stoppingToken).ConfigureAwait(false);
                return;
            }

            try
            {
                await receiver.ReceiveAsync(response, stoppingToken).ConfigureAwait(false);
                await AckAsync(message, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Let RabbitMQ redeliver after reconnect/restart.
                await NackAsync(message, requeue: true, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in ReceiveAsync for message on queue {QueueName}", _queueName);

                var retryCount = GetRetryCountFromHeaders(message.BasicProperties.Headers);

                if (retryCount < _retryCount)
                {
                    try
                    {
                        // Body is only valid inside this handler — copy before any await that might yield past reclaim.
                        var bodyCopy = message.Body.ToArray();

                        await _messagePublisher.RepublishToErrorExchangeAsync(
                                body: bodyCopy,
                                routingKey: _queueName,
                                originalProperties: message.BasicProperties,
                                nextRetryCount: retryCount + 1,
                                cancellationToken: stoppingToken)
                            .ConfigureAwait(false);

                        await AckAsync(message, stoppingToken).ConfigureAwait(false);
                    }
                    catch (Exception republishEx)
                    {
                        _logger.LogError(republishEx,
                            "Failed to republish message to error exchange for queue {QueueName}", _queueName);
                        await NackAsync(message, requeue: true, stoppingToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    try
                    {
                        await receiver.HandleErrorAsync(response, stoppingToken).ConfigureAwait(false);
                        await AckAsync(message, stoppingToken).ConfigureAwait(false);
                    }
                    catch (Exception handleErrorEx)
                    {
                        _logger.LogError(handleErrorEx,
                            "Exception in HandleErrorAsync for queue {QueueName}", _queueName);
                        await NackAsync(message, requeue: false, stoppingToken).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Unhandled exception in message handler for queue {QueueName}", _queueName);
            try
            {
                await NackAsync(message, requeue: false, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ackEx)
            {
                _logger.LogError(ackEx, "Failed to Nack message after unhandled exception.");
            }
        }
    }

    private Task AckAsync(BasicDeliverEventArgs message, CancellationToken cancellationToken)
    {
        if (_channel is not { IsOpen: true })
        {
            return Task.CompletedTask;
        }

        return _channel.BasicAckAsync(message.DeliveryTag, multiple: false, cancellationToken).AsTask();
    }

    private Task NackAsync(BasicDeliverEventArgs message, bool requeue, CancellationToken cancellationToken)
    {
        if (_channel is not { IsOpen: true })
        {
            return Task.CompletedTask;
        }

        return _channel.BasicNackAsync(message.DeliveryTag, multiple: false, requeue: requeue, cancellationToken)
            .AsTask();
    }

    private async Task StopConsumerAsync()
    {
        try
        {
            if (_channel is { IsOpen: true } && !string.IsNullOrEmpty(_consumerTag))
            {
                await _channel.BasicCancelAsync(_consumerTag).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to cancel consumer for queue {QueueName}", _queueName);
        }

        if (_channel is not null)
        {
            try
            {
                if (_channel.IsOpen)
                {
                    await _channel.CloseAsync().ConfigureAwait(false);
                }

                await _channel.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to close consumer channel for queue {QueueName}", _queueName);
            }
            finally
            {
                _channel = null;
            }
        }
    }

    private int GetRetryCountFromHeaders(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue("retry-count", out var value) || value is null)
        {
            return 0;
        }

        try
        {
            return value switch
            {
                byte[] bytes => int.Parse(Encoding.UTF8.GetString(bytes)),
                int i => i,
                long l => (int)l,
                uint ui => (int)ui,
                _ => Convert.ToInt32(value)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse retry-count header.");
            return 0;
        }
    }

    public override void Dispose()
    {
        // Connection/publisher is a shared singleton; only the consumer channel is owned here.
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
