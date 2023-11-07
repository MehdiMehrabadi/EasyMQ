using System;
using System.Threading;
using System.Threading.Tasks;

namespace EasyMQ.Abstractions;

public interface IMessagePublisher
{
    /// <summary>
    /// Publish a message to a queue based on message type
    /// </summary>
    /// <typeparam name="T">Type of message</typeparam>
    /// <param name="message">Message object</param>
    /// <param name="priority">Message priority in the queue, higher priority will process sooner</param>
    /// <param name="keepAliveTime">Message expiration in the queue (after that message will be deleted if not processed)</param>
    /// <param name="cancellationToken">Publish message will be cancelled if CancellationToken fired</param>
    /// <returns></returns>
    Task PublishAsync<T>(T message, int priority = 1, TimeSpan? keepAliveTime = null, CancellationToken cancellationToken = default) where T : class;
}
