using System.Threading;
using System.Threading.Tasks;

namespace EasyMQ.Abstractions;

public interface IReceiver<T> where T : class
{
    Task ReceiveAsync(T message, CancellationToken cancellationToken);

    Task HandleErrorAsync(T message);
}
