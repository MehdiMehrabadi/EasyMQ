using Microsoft.Extensions.DependencyInjection;

namespace EasyMQ.Abstractions;

internal class MessageBuilder : IMessageBuilder
{
    public IServiceCollection Services { get; }

    public MessageBuilder(IServiceCollection services)
    {
        Services = services;
    }
}
