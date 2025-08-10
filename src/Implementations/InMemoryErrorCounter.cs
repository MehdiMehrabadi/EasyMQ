using System;
using System.Threading.Tasks;
using EasyMQ.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace EasyMQ.Implementations
{
    public class InMemoryErrorCounter : IErrorCounter
    {
        private readonly IMemoryCache _memoryCache;
        public InMemoryErrorCounter(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        public Task<int> GetTryCountAsync(string messageId)
        {
            _memoryCache.TryGetValue(messageId, out int tryCount);
            return Task.FromResult(tryCount);

        }

        public Task UpdateTryCountAsync(string messageId, int tryCount, TimeSpan? ttl = null)
        {
            if (ttl.HasValue)
            {
                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl.Value
                };
                _memoryCache.Set(messageId, tryCount, options);
            }
            else
            {
                _memoryCache.Set(messageId, tryCount);
            }
            return Task.CompletedTask;
        }

        public Task KillCounterAsync(string messageId)
        {
            _memoryCache.Remove(messageId);
            return Task.CompletedTask;
        }
    }
}
