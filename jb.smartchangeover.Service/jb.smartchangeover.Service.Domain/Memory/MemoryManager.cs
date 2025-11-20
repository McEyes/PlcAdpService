using System;
using Jabil.Service.Frameworks.Memory;
using jb.smartchangeover.Service.Domain.Memory.Models;
using jb.smartchangeover.Service.Domain.Shared.DistributedCache;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace jb.smartchangeover.Service.Domain.Memory
{
    public class MemoryManager : SmartChangeOverDomainService
    {
        private IDistributedCachePuls _distributedCachePuls;
        private ILogger<MemoryManager> _logger;

        public MemoryManager(
            ILogger<MemoryManager> logger,
            IDistributedCachePuls distributedCachePuls
            )
        {
            _logger = logger;
            _distributedCachePuls = distributedCachePuls;
        }

        /// <summary>
        /// 存储Key值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<bool> Set<T>(string key, T value) where T : class, new()
        {
            MemoryUtils.Set(key, value, TimeSpan.MaxValue);
            return await _distributedCachePuls.KeySetAsync(key, value);

        }

        /// <summary>
        /// 获取Key值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<T> Get<T>(string key) where T : class, new()
        {
            var res = MemoryUtils.Get<T>(key);
            if (res != null)
            {
                return res;
            }
            res = await _distributedCachePuls.KeyGetAsync<T>(key);
            if (res != null)
            {
                MemoryUtils.Set(key, res, TimeSpan.MaxValue);
            }
            return res;
        }

        public async Task<bool> SetHash(string key, string hashId, string value)
        {
            var res = await Get<StringValue>(key);

            if (res == null || string.IsNullOrEmpty(res.Value))
            {
                var redisRes = await _distributedCachePuls.GetHash1Async<StringValue>(key, hashId);
                if (redisRes == null || string.IsNullOrEmpty(redisRes.Value))
                {
                    res = new StringValue
                    {
                        Value = "{}",
                    };
                }
                else
                {
                    res = redisRes;
                }
            }
            try
            {
                var obj = JObject.Parse(res.Value);
                obj[hashId] = value;

                return await Set<StringValue>(key, new StringValue
                {
                    Value = obj.ToString(),
                });
            }
            catch (Exception e)
            {
                _logger.LogError($"MemoryManager JObject Parse Failed -> {e.InnerException.Message} {e.StackTrace}");
            }
            return false;
        }

        public async Task<string?> GetHash(string key, string hashId)
        {
            var res = await Get<StringValue>(key);
            if (res == null || string.IsNullOrEmpty(res.Value))
            {
                return null;
            }

            try
            {
                var obj = JObject.Parse(res.Value);
                if (!obj.ContainsKey(hashId))
                {
                    return null;
                }
                return obj.Value<string>(hashId);
            }
            catch (Exception e)
            {
                _logger.LogError($"MemoryManager JObject Parse Failed -> {e.InnerException.Message} {e.StackTrace}");
            }

            return null;
        }

    }
}