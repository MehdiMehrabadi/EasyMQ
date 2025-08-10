
[![NuGet](https://img.shields.io/nuget/v/EasyMQ.svg)](https://www.nuget.org/packages/EasyMQ/)
[![NuGet](https://img.shields.io/nuget/dt/EasyMQ.svg)](https://www.nuget.org/packages/EasyMQ/)

## EasyMQ
This project is based on RabbitMQ and helps developers who want to avoid getting involved in the complexities of working with RabbitMQ. While working with this project and its expansion is easy, you utilize a maximum of RabbitMQ options in this project.

|TargetFramework|Support|
|---|---|
|**net9.0**|:white_check_mark:|
|**net8.0**|:white_check_mark:|
|**net7.0**|:white_check_mark:|
|**net6.0**|:white_check_mark:|
|**net5.0**|:white_check_mark:|
|**netcoreapp3.1**|:white_check_mark:|

## Features

- **Easy RabbitMQ Integration**: Simple configuration and setup
- **Retry Mechanism**: Configurable retry count with automatic error handling
- **TTL Support**: Time-based expiration for retry count data (Redis/Memory)
- **Flexible Storage**: Choose between Redis or In-Memory for retry tracking
- **Multiple .NET Versions**: Support for .NET 5.0 through .NET 9.0

## How to add in DI
You can add EasyMQ in Publisher Startup like this:
```csharp
    builder.Services.AddRabbitMq(settings =>
   {
    var configuration = builder.Configuration.GetSection("Rabbit");
    int.TryParse(configuration["Port"], out var port);
    settings.Host = configuration["Host"];
    settings.Port = port;
    settings.ExchangeName = configuration["ApplicationName"];
    settings.VirtualHost = configuration["VirtualHost"];
    settings.UserName = configuration["Username"];
    settings.Password = configuration["Password"];
  
   }, queues =>
   {
    queues.Add<MessageModel>(queueName: "message");
   // queues.Add<MessageModel2>(queueName: "message2");

   });
```

And you can  add this to Consumer Startup like this:
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
            // add queues with TTL for retry count persistence
            queues.Add<MessageModel>(queueName: "message", prefetchCount: 10, retryCount: 3, ttl: TimeSpan.FromHours(2));
            // queues.Add<MessageModel2>(queueName: "message2", prefetchCount: 3, retryCount: -1, ttl: TimeSpan.FromMinutes(30));
        }, defaultTtl: TimeSpan.FromHours(1)) // Default TTL for all queues if not specified
        // add receivers
        .AddReceiver<MessageModel, MessageReceiver>();
       //.AddReceiver<MessageModel2, MessageReceiver2>();
	
	// for set retry count message to redis or in memory 
    services.AddCacheService(configuration);
```

### TTL Feature for Retry Count Persistence

The TTL (Time To Live) feature allows you to configure how long retry count data persists in Redis or Memory. This ensures that retry count information doesn't accumulate indefinitely and automatically expires after the configured time period.

**Key Benefits:**
- **Automatic Cleanup**: Retry count data automatically expires, preventing memory/Redis accumulation
- **Flexible Configuration**: Different TTL values can be set for different queues or use a global default
- **Resource Efficiency**: Prevents memory or Redis from filling up with obsolete retry data

**Usage Examples:**

```csharp
// Global default TTL for all queues
services.AddRabbitMq(settings => { /* config */ }, queues => { /* queues */ }, 
    defaultTtl: TimeSpan.FromHours(1));

// Per-queue TTL configuration
queues.Add<MessageModel>("high-priority", retryCount: 5, ttl: TimeSpan.FromHours(2));
queues.Add<MessageModel>("low-priority", retryCount: 2, ttl: TimeSpan.FromMinutes(30));

// No TTL (indefinite persistence)
queues.Add<MessageModel>("message", retryCount: 3); // Uses default TTL if specified
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

## Documentation

For detailed information about the TTL feature and advanced usage examples, see [TTL_FEATURE_README.md](TTL_FEATURE_README.md).

In this project, an attempt has been made to make working with RabbitMQ more convenient. Now, if you have any suggestions, you are welcome to contribute in the project.


