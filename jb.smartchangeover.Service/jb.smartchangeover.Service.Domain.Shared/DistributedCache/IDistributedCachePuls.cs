using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jb.smartchangeover.Service.Domain.Shared.DistributedCache
{
    public interface IDistributedCachePuls : IDistributedCache
    {

        /// <summary>
        /// Key存储
        /// </summary>
        public Task<bool> KeySetAsync<T>(string key, T value) where T : class, new();

        /// <summary>
        /// Key获取
        /// </summary>
        public Task<T> KeyGetAsync<T>(string key) where T : class;


        /// <summary>
        /// 存储数据到hash表
        /// </summary>
        public Task<long> Increment(string hashId, string key);

        /// <summary>
        /// Key是否存在
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        Task<bool> KeyExistsAsync(string key);

        /// <summary>
        /// 存储数据到hash表
        /// </summary>
        public Task<bool> SetHashAsync<T>(string hashId, string key, T t);

        /// <summary>
        /// 移除hash中的某值
        /// </summary>
        public Task<bool> HashRemoveAsync(string hashId, string key);

        /// <summary>
        /// 设置Key过期时间
        /// </summary>
        public Task<bool> KeyExpireAsync(string key, TimeSpan ttl);

        /// <summary>
        /// 存取任意类型的值(hashId与key相同)
        /// </summary>
        public Task<List<T>> GetHashAllAsync<T>(string key) where T : class;

        /// <summary>
        /// 取字符串类型的值(hashId与key相同)
        /// </summary>
        public Task<List<string>> GetHashAllStrAsync<T>(string key);

        /// <summary>
        /// 获取hash所有key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Task<List<string>> GetHashKeysAsync(string key);

        /// <summary>
        /// 获取hash值
        /// </summary>
        /// <param name="key"></param>
        /// <returns>default null</returns>
        public Task<T> GetHashAsync<T>(string key, string hashField) where T : class;

        /// <summary>
        /// 获取hash
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="hashfield"></param>
        /// <returns>new T</returns>
        Task<T> GetHash1Async<T>(string key, string hashfield) where T : class, new();
    }
}
