using System;
using System.Collections.Generic;

namespace EasyMQ.Abstractions;

public class QueueSettings
{
    private readonly Dictionary<Type, QueueRegistration> _queues = new();

    internal IReadOnlyDictionary<Type, QueueRegistration> Queues => _queues;

    /// <summary>
    /// Register a queue for a message type.
    /// </summary>
    /// <typeparam name="T">Type of the message you want to enqueue</typeparam>
    /// <param name="queueName">Queue name (defaults to the message type full name)</param>
    /// <param name="prefetchCount">Number of unacked messages fetched from RabbitMQ for this consumer</param>
    /// <param name="retryCount">Number of retries before <c>HandleErrorAsync</c> is called</param>
    public void Add<T>(string queueName = null, int prefetchCount = 1, uint retryCount = 3) where T : class
    {
        if (prefetchCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(prefetchCount), "prefetchCount must be at least 1.");
        }

        var type = typeof(T);
        if (!_queues.TryAdd(type, new QueueRegistration(
                queueName ?? type.FullName!,
                prefetchCount,
                retryCount,
                type)))
        {
            throw new InvalidOperationException(
                $"A queue is already registered for message type '{type.FullName}'.");
        }
    }

    internal QueueRegistration GetRequired<T>() where T : class
    {
        if (_queues.TryGetValue(typeof(T), out var registration))
        {
            return registration;
        }

        throw new InvalidOperationException(
            $"No queue is registered for message type '{typeof(T).FullName}'. Call queues.Add<{typeof(T).Name}>() during setup.");
    }
}

internal sealed record QueueRegistration(string Name, int PrefetchCount, uint RetryCount, Type Type);
