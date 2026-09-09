using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using EasyMQ.Abstractions;
using EasyMQ.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EasyMQ;

public static class ServiceCollectionExtension
{
    /// <summary>
    /// Registers EasyMQ publisher, connection, and queue topology configuration.
    /// </summary>
    public static IMessageBuilder AddEasyMq(
        this IServiceCollection services,
        Action<MessageManagerSettings> messageManagerConfiguration,
        Action<QueueSettings> queuesConfiguration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(messageManagerConfiguration);
        ArgumentNullException.ThrowIfNull(queuesConfiguration);

        var messageManagerSettings = new MessageManagerSettings();
        messageManagerConfiguration(messageManagerSettings);

        messageManagerSettings.JsonSerializerOptions ??= new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        if (string.IsNullOrWhiteSpace(messageManagerSettings.ExchangeName))
        {
            throw new InvalidOperationException("ExchangeName is required. Set settings.ExchangeName in AddEasyMq.");
        }

        if (string.IsNullOrWhiteSpace(messageManagerSettings.Host))
        {
            throw new InvalidOperationException("Host is required. Set settings.Host in AddEasyMq.");
        }

        var queueSettings = new QueueSettings();
        queuesConfiguration(queueSettings);

        services.AddSingleton(messageManagerSettings);
        services.AddSingleton(queueSettings);
        services.AddSingleton<MessagePublisher>();
        services.AddSingleton<IMessagePublisher>(provider => provider.GetRequiredService<MessagePublisher>());

        services.Configure<HostOptions>(opts =>
            opts.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

        return new MessageBuilder(services);
    }

    /// <summary>
    /// Registers EasyMQ publisher, connection, and queue topology configuration.
    /// </summary>
    [Obsolete("Use AddEasyMq instead.")]
    public static IMessageBuilder AddRabbitMq(
        this IServiceCollection services,
        Action<MessageManagerSettings> messageManagerConfiguration,
        Action<QueueSettings> queuesConfiguration)
        => AddEasyMq(services, messageManagerConfiguration, queuesConfiguration);

    /// <summary>
    /// Registers a background consumer for <typeparamref name="TObject"/> using <typeparamref name="TReceiver"/>.
    /// </summary>
    public static IMessageBuilder AddReceiver<TObject, TReceiver>(this IMessageBuilder builder)
        where TObject : class
        where TReceiver : class, IReceiver<TObject>
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHostedService<Listener<TObject>>();
        builder.Services.AddScoped<IReceiver<TObject>, TReceiver>();
        return builder;
    }
}
