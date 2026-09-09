#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EasyMQ.Abstractions;
using RabbitMQ.Client;

namespace EasyMQ.Core;

internal sealed class MessagePublisher : IMessagePublisher, IAsyncDisposable, IDisposable
{
    private const string MaxPriorityHeader = "x-max-priority";
    private const string DeadLetterExchange = "x-dead-letter-exchange";
    private const string MessageTtl = "x-message-ttl";

    private readonly MessageManagerSettings _settings;
    private readonly QueueSettings _queueSettings;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly SemaphoreSlim _republishLock = new(1, 1);

    private IConnection? _connection;
    private IChannel? _publishChannel;
    private IChannel? _republishChannel;
    private bool _initialized;
    private bool _disposed;

    public MessagePublisher(MessageManagerSettings settings, QueueSettings queueSettings)
    {
        _settings = settings;
        _queueSettings = queueSettings;
    }

    internal async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized && IsUsable(_publishChannel) && IsUsable(_republishChannel) && _connection is { IsOpen: true })
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_initialized && IsUsable(_publishChannel) && IsUsable(_republishChannel) && _connection is { IsOpen: true })
            {
                return;
            }

            await DisposeChannelsAsync().ConfigureAwait(false);

            if (_connection is null || !_connection.IsOpen)
            {
                if (_connection is not null)
                {
                    await _connection.DisposeAsync().ConfigureAwait(false);
                }

                var factory = new ConnectionFactory
                {
                    HostName = _settings.Host,
                    Port = _settings.Port,
                    UserName = _settings.UserName,
                    Password = _settings.Password,
                    VirtualHost = _settings.VirtualHost,
                    AutomaticRecoveryEnabled = true,
                    TopologyRecoveryEnabled = true
                };

                _connection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
            }

            _publishChannel = await CreateChannelAsync(publisherConfirms: _settings.EnablePublisherConfirms, cancellationToken)
                .ConfigureAwait(false);
            _republishChannel = await CreateChannelAsync(publisherConfirms: false, cancellationToken)
                .ConfigureAwait(false);

            await DeclareTopologyAsync(cancellationToken).ConfigureAwait(false);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    internal async Task<IChannel> CreateConsumerChannelAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        // Consumers do not need publisher confirms; keep the channel dedicated and lightweight.
        return await CreateChannelAsync(publisherConfirms: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task PublishAsync<T>(
        T message,
        int priority = 1,
        TimeSpan? keepAliveTime = null,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var registration = _queueSettings.GetRequired<T>();
        var body = JsonSerializer.SerializeToUtf8Bytes(
            message,
            _settings.JsonSerializerOptions ?? JsonOptions.Default);

        var properties = new BasicProperties
        {
            Persistent = true,
            Priority = Convert.ToByte(Math.Clamp(priority, 0, 10)),
            Expiration = keepAliveTime?.TotalMilliseconds.ToString(CultureInfo.InvariantCulture),
            MessageId = Guid.NewGuid().ToString("N"),
            Headers = new Dictionary<string, object?>
            {
                ["retry-count"] = 0
            }
        };

        cancellationToken.ThrowIfCancellationRequested();
        await _publishChannel!.BasicPublishAsync(
                _settings.ExchangeName,
                registration.Name,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RepublishToErrorExchangeAsync(
        ReadOnlyMemory<byte> body,
        string routingKey,
        IReadOnlyBasicProperties originalProperties,
        int nextRetryCount,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var headers = CopyHeaders(originalProperties.Headers);
        headers["retry-count"] = nextRetryCount;

        var properties = new BasicProperties
        {
            Persistent = true,
            Priority = originalProperties.Priority,
            Expiration = originalProperties.Expiration,
            MessageId = originalProperties.MessageId,
            Headers = headers
        };

        // One shared republish channel, serialized — avoids open/close per failure.
        await _republishLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsUsable(_republishChannel))
            {
                _republishChannel = await CreateChannelAsync(publisherConfirms: false, cancellationToken)
                    .ConfigureAwait(false);
            }

            await _republishChannel!.BasicPublishAsync(
                    $"{_settings.ExchangeName}_error",
                    routingKey,
                    mandatory: false,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _republishLock.Release();
        }
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            await DisposeChannelsAsync().ConfigureAwait(false);

            if (_connection is not null)
            {
                if (_connection.IsOpen)
                {
                    await _connection.CloseAsync().ConfigureAwait(false);
                }

                await _connection.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch
        {
            // ignored on shutdown
        }

        _initLock.Dispose();
        _republishLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task DeclareTopologyAsync(CancellationToken cancellationToken)
    {
        var channel = _publishChannel ?? throw new InvalidOperationException("Publish channel is not initialized.");
        var exchangeError = $"{_settings.ExchangeName}_error";
        var errorTtl = Math.Max(0, _settings.ErrorQueueMessageTtlMilliseconds);

        await channel.ExchangeDeclareAsync(
                _settings.ExchangeName,
                ExchangeType.Direct,
                durable: true,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        await channel.ExchangeDeclareAsync(
                exchangeError,
                ExchangeType.Direct,
                durable: true,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        foreach (var queue in _queueSettings.Queues.Values)
        {
            var queueError = $"{queue.Name}_error";
            var args = new Dictionary<string, object?>
            {
                [MaxPriorityHeader] = 10,
                [DeadLetterExchange] = exchangeError,
            };

            await channel.QueueDeclareAsync(
                    queue.Name,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: args,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await channel.QueueBindAsync(
                    queue.Name,
                    _settings.ExchangeName,
                    queue.Name,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var errorArgs = new Dictionary<string, object?>
            {
                [MaxPriorityHeader] = 10,
                [DeadLetterExchange] = _settings.ExchangeName,
                [MessageTtl] = errorTtl,
            };

            await channel.QueueDeclareAsync(
                    queueError,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: errorArgs,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await channel.QueueBindAsync(
                    queueError,
                    exchangeError,
                    queue.Name,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private Task<IChannel> CreateChannelAsync(bool publisherConfirms, CancellationToken cancellationToken)
    {
        var options = new CreateChannelOptions(
            publisherConfirmationsEnabled: publisherConfirms,
            publisherConfirmationTrackingEnabled: publisherConfirms);

        return _connection!.CreateChannelAsync(options, cancellationToken);
    }

    private async Task DisposeChannelsAsync()
    {
        _initialized = false;

        if (_publishChannel is not null)
        {
            try
            {
                if (_publishChannel.IsOpen)
                {
                    await _publishChannel.CloseAsync().ConfigureAwait(false);
                }

                await _publishChannel.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            _publishChannel = null;
        }

        if (_republishChannel is not null)
        {
            try
            {
                if (_republishChannel.IsOpen)
                {
                    await _republishChannel.CloseAsync().ConfigureAwait(false);
                }

                await _republishChannel.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            _republishChannel = null;
        }
    }

    private static bool IsUsable(IChannel? channel) => channel is { IsOpen: true };

    private static Dictionary<string, object?> CopyHeaders(IDictionary<string, object?>? headers)
    {
        var copy = new Dictionary<string, object?>();
        if (headers is null)
        {
            return copy;
        }

        foreach (var pair in headers)
        {
            copy[pair.Key] = pair.Value;
        }

        return copy;
    }
}
