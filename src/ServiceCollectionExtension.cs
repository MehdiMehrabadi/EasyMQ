using System;
using EasyMQ.Abstractions;
using EasyMQ.Core;
using EasyMQ.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace EasyMQ
{
    public static class ServiceCollectionExtension
    {
        public static IMessageBuilder AddRabbitMq(this IServiceCollection services, Action<MessageManagerSettings> messageManagerConfiguration, Action<QueueSettings> queuesConfiguration)
        {
            services.AddSingleton<MessagePublisher>();
            services.AddSingleton<IMessagePublisher>(provider => provider.GetService<MessagePublisher>());

            var messageManagerSettings = new MessageManagerSettings();
            messageManagerConfiguration.Invoke(messageManagerSettings);
            services.AddSingleton(messageManagerSettings);

            var queueSettings = new QueueSettings();
            queuesConfiguration.Invoke(queueSettings);
            services.AddSingleton(queueSettings);

            return new MessageBuilder(services);
        }

        public static IMessageBuilder AddReceiver<TObject, TReceiver>(this IMessageBuilder builder) where TObject : class where TReceiver : class, IReceiver<TObject>
        {
            builder.Services.AddHostedService<Listener<TObject>>();
            builder.Services.AddScoped<IReceiver<TObject>, TReceiver>();
            return builder;
        }
        

    }
}
