using System;
using System.Threading;
using System.Threading.Tasks;

namespace EasyMQ.Abstractions;

public interface IMessagePublisher
{
    /// <summary>
    /// Publishes a message to the queue registered for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Type of message</typeparam>
    /// <param name="message">Message object</param>
    /// <param name="priority">Priority (0-10). Higher values are processed sooner.</param>
    /// <param name="keepAliveTime">Optional per-message TTL; expired messages are dropped if not consumed.</param>
    /// <param name="cancellationToken">Cancels the publish operation</param>
    Task PublishAsync<T>(T message, int priority = 1, TimeSpan? keepAliveTime = null, CancellationToken cancellationToken = default) where T : class;
}
