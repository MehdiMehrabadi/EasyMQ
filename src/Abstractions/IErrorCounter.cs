using System;
using System.Threading.Tasks;

namespace EasyMQ.Abstractions
{
    public interface IErrorCounter
    {
        Task<int> GetTryCountAsync(string messageId);
        Task UpdateTryCountAsync(string messageId, int tryCount, TimeSpan? ttl = null);
        Task KillCounterAsync(string messageId);
    }
}
    