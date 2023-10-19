using System;
using System.Collections.Generic;

namespace EasyMQ.Abstractions;

public class QueueSettings
{
    internal IList<(string Name, int prefetchCount, int retryCount, Type Type)> Queues { get; } = new List<(string, int, int, Type)>();

    public void Add<T>(string queueName = null, int prefetchCount = 1, int retryCount = 1) where T : class
    {
        var type = typeof(T);
        Queues.Add((queueName ?? type.FullName, prefetchCount, retryCount, type));  

    }
}
