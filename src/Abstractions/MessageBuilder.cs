using Microsoft.Extensions.DependencyInjection;

namespace EasyMQ.Abstractions;

internal class MessageBuilder(IServiceCollection services) : IMessageBuilder
{
    public IServiceCollection Services { get; } = services;
}
