using System;
using System.Threading.Tasks;

namespace EasyMQ.Abstractions;

public interface IMessagePublisher
{
    /// <summary>
    /// Publish a message to a queue based on message type
    /// </summary>
    /// <typeparam name="T">type of message</typeparam>
    /// <param name="message">message object</param>
    /// <param name="priority">message priority in the queue, higher priority will process sooner</param>
    /// <param name="keepAliveTime">message expiration in the queue (after that message will be deleted if not processed)</param>
    /// <returns></returns>
    Task PublishAsync<T>(T message, int priority = 1, TimeSpan? keepAliveTime = null) where T : class;
}
