using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackstageTask_Second
{
    class RedisHelper
    {
        //不写端口，默认6397
        static ConfigurationOptions configurationOptions = ConfigurationOptions.Parse("172.20.70.20" + ":" + "6379");
        static ConnectionMultiplexer redisConn;
        public static ConnectionMultiplexer RedisConn
        {
            get
            {
                return ConnectionMultiplexer.Connect(configurationOptions);
            }
        }
    }
}
