using System.Threading;
using System.Threading.Tasks;

namespace EasyMQ.Abstractions;

public interface IReceiver<T> where T : class
{
    /// <summary>
    /// To process a message from queue
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task ReceiveAsync(T message, CancellationToken cancellationToken);

    /// <summary>
    /// In case of message still get an exception after retry count, 
    /// You can handle the error by your logic
    /// </summary>
    /// <param name="message">Message object</param>
    /// <returns></returns>
    Task HandleErrorAsync(T message);
}
