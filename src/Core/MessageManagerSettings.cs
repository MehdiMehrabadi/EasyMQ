using System.Text.Json;
using EasyMQ.Abstractions;

namespace EasyMQ.Core;

public class MessageManagerSettings
{
    public string Host { get; set; }
    public int Port { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
    public string VirtualHost { get; set; }
    public string ExchangeName { get; set; }
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = JsonOptions.Default;

    /// <summary>
    /// Delay (ms) before a failed message returns from the error queue to the main queue.
    /// Default is 10 seconds. Changing this for an existing queue requires deleting that queue in RabbitMQ.
    /// </summary>
    public int ErrorQueueMessageTtlMilliseconds { get; set; } = 10_000;

    /// <summary>
    /// When true (default), publish waits for broker confirmation.
    /// </summary>
    public bool EnablePublisherConfirms { get; set; } = true;
}
