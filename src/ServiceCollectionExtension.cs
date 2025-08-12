using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using EasyMQ.Abstractions;
using EasyMQ.Core;
using Microsoft.Extensions.DependencyInjection;

namespace EasyMQ
{
    public static class ServiceCollectionExtension
    {
        public static IMessageBuilder AddRabbitMq(
            this IServiceCollection services,
            Action<MessageManagerSettings> messageManagerConfiguration,
            Action<QueueSettings> queuesConfiguration)
        {
            services.AddSingleton<MessagePublisher>();
            services.AddSingleton<IMessagePublisher>(provider => provider.GetRequiredService<MessagePublisher>());
            var messageManagerSettings = new MessageManagerSettings();
            messageManagerConfiguration.Invoke(messageManagerSettings);
            services.AddSingleton(messageManagerSettings);
            var queueSettings = new QueueSettings();
            queuesConfiguration.Invoke(queueSettings);
            services.AddSingleton(queueSettings);

            messageManagerSettings.JsonSerializerOptions ??= new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            return new MessageBuilder(services);
        }

        public static IMessageBuilder AddReceiver<TObject, TReceiver>(this IMessageBuilder builder)
            where TObject : class
            where TReceiver : class, IReceiver<TObject>
        {
            builder.Services.AddHostedService<Listener<TObject>>();
            builder.Services.AddScoped<IReceiver<TObject>, TReceiver>();
            return builder;
        }
    }
}