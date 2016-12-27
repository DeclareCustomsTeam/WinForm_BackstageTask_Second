
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

namespace BackstageTask_Second
{

    public static class SeRedis
    {

        private static string constr = ConfigurationManager.AppSettings["redisserver"];

        private static Lazy<ConnectionMultiplexer> lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
        {
            return ConnectionMultiplexer.Connect(constr);

        });

        public static ConnectionMultiplexer redis
        {
            get
            {
                return lazyConnection.Value;
            }
        }
    }
}