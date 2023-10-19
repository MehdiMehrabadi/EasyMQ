using System;
using System.Threading.Tasks;

namespace EasyMQ.Abstractions;

public interface IMessagePublisher
{
    Task PublishAsync<T>(T message, int priority = 1, TimeSpan? keepAliveTime = null) where T : class;
}
