using System;
using System.Collections.Generic;

namespace EasyMQ.Abstractions;

public class QueueSettings
{
    internal IList<(string Name, int prefetchCount,uint retryCount, Type Type)> Queues { get; } = new List<(string, int, uint, Type)>();

    /// <summary>
    /// Create a queue
    /// </summary>
    /// <typeparam name="T">Type of the message you want to enqueue</typeparam>
    /// <param name="queueName">Queue name</param>
    /// <param name="prefetchCount">Number of messages that app will fetch from rabbit</param>
    /// <param name="retryCount">Number of retry to process message in case of any errors</param>
    public void Add<T>(string queueName = null, int prefetchCount = 1, uint retryCount = 3) where T : class
    {
        var type = typeof(T);
        Queues.Add((queueName ?? type.FullName, prefetchCount, retryCount, type));

    }
}
