
[![NuGet](https://img.shields.io/nuget/v/EasyMQ.svg)](https://www.nuget.org/packages/EasyMQ/)
[![NuGet](https://img.shields.io/nuget/dt/EasyMQ.svg)](https://www.nuget.org/packages/EasyMQ/)

## EasyMQ
This project is based on RabbitMQ and helps developers who want to avoid getting involved in the complexities of working with RabbitMQ. While working with this project and its expansion is easy, you utilize a maximum of RabbitMQ options in this project.

|TargetFramework|Support|
|---|---|
|**net7.0**|:white_check_mark:|
|**net6.0**|:white_check_mark:|
|**net5.0**|:white_check_mark:|
|**netcoreapp3.1**|:white_check_mark:|


## How to add in DI
You can add EasyMQ in Startup like this:
```csharp
    services.AddRabbitMq(settings =>
        {
            var config = configuration.GetSection("Rabbit");
            int.TryParse(config["Port"], out var port);
            settings.Host = config["Host"];
            settings.Port = port;
            settings.ExchangeName = config["ApplicationName"];
            settings.VirtualHost = config["VirtualHost"];
            settings.UserName = config["Username"];
            settings.Password = config["Password"];
        }, queues =>
        {
            queues.Add<MessageModel>(queueName: "message", prefetchCount: 10, retryCount: 3);
            queues.Add<MessageModel2>(queueName: "message2", prefetchCount: 3, retryCount: -1);
        })
        // add receivers
        .AddReceiver<MessageModel, MessageReceiver>()
        .AddReceiver<MessageModel2, MessageReceiver2>();
```

As evident, you can specify the prefetch count for each queue, and set even define the retry count in case of an error. The mechanism of this project is that in case of an error in any part of the message, it adds it to the error queue of the same queue and returns it to the main queue after 10 seconds, and this count is configurable by yourself.
Even if you have an issue with the 10-second interval, you can change this time.
However, please note that if you change the time, you need to delete the queue from RabbitMQ and run the program again to recreate the queue. 
If you want count of errors to be retried infinitely, you can use the number -1.
You can use the following code to publish the message in the queue:

## Publisher
```csharp

      [HttpPost("send")]
      public async Task<IActionResult> SendMessageAsync([FromBody] MessageRequest request)
      {
          await messagePublisher.PublishAsync(request.Adapt<MessageModel>(), priority: 1, keepAliveTime: TimeSpan.FromMinutes(10), HttpContext.RequestAborted);
          return Ok();
      }

```
And you can use the following code to consume message in the queue:
## Consumer
```csharp
public class MessageReceiver : IReceiver<MessageModel>
{
    public async Task ReceiveAsync(MessageModel message, CancellationToken cancellationToken)
    {
        try
        {
            //throw new Exception();// If you want to test retry count, uncomment this code!
            Console.WriteLine($"received: {message.Receiver} ,text: {message.Text}");
            await Task.Yield();
        }
        catch (Exception)
        {
            throw new Exception();
        }
    }

    /// <summary>
    /// this method is running after finishing retry error count.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken">Task CancellationToken</param>
    /// <returns></returns>
    public async Task HandleErrorAsync(MessageModel message, CancellationToken cancellationToken)
    {
        // Implementing your own scenarios. for example: save to db for check later, save to file or ....
        Console.WriteLine($"message: {message.Receiver} ,text: {message.Text} , saved to db");
        await Task.Yield();
    }
}
```
### Custom configuration for publisher and consumer
Rabbit option in appsettings.json
```csharp
  "Rabbit": {
    "Host": "localhost",
    "Port": "5672",
    "Username": "guest",
    "Password": "guest",
    "VirtualHost": "/",
    "ApplicationName": "EasyMq"
  },
```


### And Custom configuration for consumer
CacheOption in appsettings.json
```csharp
  "CacheOption": {
    "UseRedis": false, //if set false , default save messageId to in memory
    "RedisConnectionString": "localhost:6379",
    "RedisPassword": ""
  }
```
In this project, an attempt has been made to make working with RabbitMQ more convenient. Now, if you have any suggestions, you are welcome to contribute in the project.


