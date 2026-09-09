
[![NuGet](https://img.shields.io/nuget/v/EasyMQ.svg)](https://www.nuget.org/packages/EasyMQ/)
[![NuGet](https://img.shields.io/nuget/dt/EasyMQ.svg)](https://www.nuget.org/packages/EasyMQ/)

## EasyMQ
This project is based on RabbitMQ and helps developers who want to avoid getting involved in the complexities of working with RabbitMQ. While working with this project and its expansion is easy, you utilize a maximum of RabbitMQ options in this project.

| Target Framework | Support |
|------------------|---------|
| **net10.0**       | ✅      |
| **net9.0**       | ✅      |
| **net8.0**       | ✅      |

## What's new (v1.2.0)

- **`AddEasyMq`** — preferred DI entry point (replaces `AddRabbitMq`; old name still works but is obsolete)
- **`EnablePublisherConfirms`** — waits for RabbitMQ broker confirmation on publish (default: `true`)
- **`ErrorQueueMessageTtlMilliseconds`** — configures how long failed messages stay in the error queue before retry (default: `10000`)
- Compatible with **RabbitMQ.Client 7.x** (async API)

## How to add in DI
You can add EasyMQ in Publisher Startup like this:
```csharp
builder.Services.AddEasyMq(settings =>
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

And you can add this to Consumer Startup like this:
```csharp
services.AddEasyMq(settings =>
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
    queues.Add<MessageModel>(queueName: "message", prefetchCount: 10, retryCount: 5);
    // queues.Add<MessageModel2>(queueName: "message2", prefetchCount: 3);
})
.AddReceiver<MessageModel, MessageReceiver>();
//.AddReceiver<MessageModel2, MessageReceiver2>();
```

You can specify `prefetchCount` and `retryCount` per queue. On processing errors, the message is moved to the related error queue and returns to the main queue after a delay (default: 10 seconds). After retries are exhausted, `HandleErrorAsync` is called.

## Optional settings

These are optional. Defaults are production-safe; change them only when you need to.

| Setting | Default | Description |
|---------|---------|-------------|
| `EnablePublisherConfirms` | `true` | When enabled, `PublishAsync` waits until RabbitMQ confirms that it accepted the message. If the broker rejects it (or the confirm fails), an exception is thrown instead of a silent loss. Disable only if you prioritize maximum publish throughput over delivery confirmation. |
| `ErrorQueueMessageTtlMilliseconds` | `10000` | Delay (ms) before a failed message returns from the error queue to the main queue. |

```csharp
settings.EnablePublisherConfirms = true;                 // default
settings.ErrorQueueMessageTtlMilliseconds = 10000;       // default: 10 seconds
```

> **Important — changing retry delay after first run:**
> RabbitMQ does **not** allow changing queue arguments (including `x-message-ttl`) on an existing queue.
> If you change `ErrorQueueMessageTtlMilliseconds` and restart the app, declare will fail with `406 PRECONDITION_FAILED` and the app may not start.
>
> **What to do:**
> 1. Stop the app
> 2. Delete the related `*_error` queue(s) in RabbitMQ Management UI (or via CLI)
> 3. Start the app again so EasyMQ recreates them with the new TTL
>
> Tip: pick a stable TTL before going to production to avoid this operational step.

## Publisher
```csharp
[HttpPost("send")]
public async Task<IActionResult> SendMessageAsync([FromBody] MessageRequest request)
{
    await messagePublisher.PublishAsync(
        request.Adapt<MessageModel>(),
        priority: 1,
        keepAliveTime: TimeSpan.FromMinutes(10),
        HttpContext.RequestAborted);
    return Ok();
}
```

## Consumer
```csharp
public class MessageReceiver : IReceiver<MessageModel>
{
    public async Task ReceiveAsync(MessageModel message, CancellationToken cancellationToken)
    {
        // throw new Exception(); // uncomment to test retry behavior
        Console.WriteLine($"received: {message.Receiver} ,text: {message.Text}");
        await Task.Yield();
    }

    /// <summary>
    /// Called after retry count is exhausted.
    /// </summary>
    public async Task HandleErrorAsync(MessageModel message, CancellationToken cancellationToken)
    {
        // e.g. save to db / file for later inspection
        Console.WriteLine($"message: {message.Receiver} ,text: {message.Text} , saved to db");
        await Task.Yield();
    }
}
```

### Custom configuration for publisher and consumer
Rabbit option in `appsettings.json`:
```json
"Rabbit": {
  "Host": "localhost",
  "Port": "5672",
  "Username": "guest",
  "Password": "guest",
  "VirtualHost": "/",
  "ApplicationName": "EasyMq"
}
```

In this project, an attempt has been made to make working with RabbitMQ more convenient. Now, if you have any suggestions, you are welcome to contribute in the project.
