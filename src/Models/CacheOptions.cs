using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EasyMQ.Models
{
    public sealed class CacheOptions
    {
        public bool UseRedis { get; set; }
        public string RedisConnectionString { get; set; }
        public string RedisPassword { get; set; }

    }
}
