using Microsoft.Extensions.DependencyInjection;

namespace EasyMQ.Abstractions;

public interface IMessageBuilder
{
    IServiceCollection Services { get; }
}
