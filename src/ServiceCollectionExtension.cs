using System;
using EasyMQ.Abstractions;
using EasyMQ.Core;
using EasyMQ.Implementations;
using EasyMQ.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace EasyMQ
{
    public static class ServiceCollectionExtension
    {
        public static IMessageBuilder AddRabbitMq(this IServiceCollection services, Action<MessageManagerSettings> messageManagerConfiguration, Action<QueueSettings> queuesConfiguration, TimeSpan? defaultTtl = null)
        {
            services.AddSingleton<MessagePublisher>();
            services.AddSingleton<IMessagePublisher>(provider => provider.GetService<MessagePublisher>());

            var messageManagerSettings = new MessageManagerSettings();
            messageManagerConfiguration.Invoke(messageManagerSettings);
            services.AddSingleton(messageManagerSettings);

            var queueSettings = new QueueSettings();
            queuesConfiguration.Invoke(queueSettings);
            services.AddSingleton(queueSettings);

            // Add default TTL configuration if provided
            if (defaultTtl.HasValue)
            {
                services.AddSingleton(new DefaultTtlConfiguration { DefaultTtl = defaultTtl.Value });
            }

            return new MessageBuilder(services);
        }

        public static IMessageBuilder AddReceiver<TObject, TReceiver>(this IMessageBuilder builder) where TObject : class where TReceiver : class, IReceiver<TObject>
        {
            builder.Services.AddHostedService<Listener<TObject>>();
            builder.Services.AddScoped<IReceiver<TObject>, TReceiver>();
            return builder;
        }

        public static void AddCacheService(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<CacheOptions>(configuration.GetSection("CacheOption"));
            var options = new CacheOptions();
            configuration.GetSection("CacheOption").Bind(options);
            if (options.UseRedis)
            {
                services.AddScoped<IErrorCounter, RedisErrorCounter>();


                var redisOptions = ConfigurationOptions.Parse(options.RedisConnectionString);
                redisOptions.Password = options.RedisPassword;

                services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisOptions));

                services.AddScoped<IDatabase>(provider =>
                {
                    var connection = provider.GetRequiredService<IConnectionMultiplexer>();
                    return connection.GetDatabase();
                });
            }
            else
            {
                services.AddMemoryCache();
                services.AddScoped<IErrorCounter, InMemoryErrorCounter>();
            }
        }

    }

    // Configuration class for default TTL
    public class DefaultTtlConfiguration
    {
        public TimeSpan DefaultTtl { get; set; }
    }
}
