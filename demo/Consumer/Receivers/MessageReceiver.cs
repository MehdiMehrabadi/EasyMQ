using Common;
using EasyMQ.Abstractions;
using RabbitMQ.Client.Events;

namespace Consumer.Receivers;

public class MessageReceiver : IReceiver<MessageModel>
{
    public async Task ReceiveAsync(MessageModel message, CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine($"received: {message.Receiver} ,text: {message.Text}");
            throw new Exception();// If you want to test retry count, uncomment this code!
            Console.WriteLine($"received: {message.Receiver} ,text: {message.Text}");
            await Task.Yield();
        }
        catch (Exception)
        {
            throw new Exception();
        }
    }

    /// <summary>
    /// this method is running after finishing retry count.
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public async Task HandleErrorAsync(MessageModel message,CancellationToken cancellationToken)
    {
        try
        {
            // Implementing your own scenarios. for example: save to db for check later, save to file or ....
            Console.WriteLine($"message: {message.Receiver} ,text: {message.Text} , saved to db"); 
         
            await Task.Yield();
        }
        catch (Exception)
        {
            throw new Exception();
        }
    }
}
