using System;
using System.Threading.Tasks;
using EasyMQ.Abstractions;
using StackExchange.Redis;

namespace EasyMQ.Implementations
{
    public class RedisErrorCounter : IErrorCounter
    {
        private readonly IDatabase _redisDb;

        public RedisErrorCounter(IDatabase redisDb)
        {
            this._redisDb = redisDb;
        }

        public async Task<int> GetTryCountAsync(string messageId)
        {
            var count = await _redisDb.StringGetAsync(messageId);
            return count.IsNull ? 0 : (int)count;
        }

        public async Task UpdateTryCountAsync(string messageId, int tryCount, TimeSpan? ttl = null)
        {
            if (ttl.HasValue)
            {
                await _redisDb.StringSetAsync(messageId, tryCount, ttl.Value);
            }
            else
            {
                await _redisDb.StringSetAsync(messageId, tryCount);
            }
        }   

        public async Task KillCounterAsync(string messageId)
        {
            await _redisDb.KeyDeleteAsync(messageId);
        }
    }
}
