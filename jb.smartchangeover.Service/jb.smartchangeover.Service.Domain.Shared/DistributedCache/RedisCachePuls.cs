using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace jb.smartchangeover.Service.Domain.Shared.DistributedCache
{
    public class RedisCachePuls : RedisCache, IDistributedCachePuls, IDisposable
    {
        private const long NotPresent = -1;
        private static readonly Version ServerVersionWithExtendedSetCommand = new Version(4, 0, 0);
        private volatile IConnectionMultiplexer _connection;
        private IDatabase _cache;
        private bool _disposed;
        private readonly RedisCacheOptions _options;
        private readonly string _instance;
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(initialCount: 1, maxCount: 1);

        private readonly ILogger<RedisCachePuls> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="RedisCache"/>.
        /// </summary>
        /// <param name="optionsAccessor">The configuration options.</param>
        public RedisCachePuls(
            IOptions<RedisCacheOptions> optionsAccessor,
            ILogger<RedisCachePuls> logger) : base(optionsAccessor)
        {
            _logger = logger;
            if (optionsAccessor == null)
            {
                throw new ArgumentNullException(nameof(optionsAccessor));
            }

            _options = optionsAccessor.Value;

            // This allows partitioning a single backend cache for use with multiple apps/services.
            _instance = _options.InstanceName ?? string.Empty;
            Connect();
        }

        private void Connect()
        {
            CheckDisposed();
            if (_cache != null)
            {
                return;
            }
            _connectionLock.Wait();
            try
            {
                if (_cache == null)
                {
                    if (_options.ConnectionMultiplexerFactory == null)
                    {
                        if (_options.ConfigurationOptions is not null)
                        {
                            _connection = ConnectionMultiplexer.Connect(_options.ConfigurationOptions);
                        }
                        else
                        {
                            _connection = ConnectionMultiplexer.Connect(_options.Configuration);
                        }
                    }
                    else
                    {
                        _connection = _options.ConnectionMultiplexerFactory().GetAwaiter().GetResult();
                    }
                    PrepareConnection();
                    _cache = _connection.GetDatabase();
                }
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private void PrepareConnection()
        {
            ValidateServerFeatures();
            TryRegisterProfiler();
        }

        private void ValidateServerFeatures()
        {
            _ = _connection ?? throw new InvalidOperationException($"{nameof(_connection)} cannot be null.");

            foreach (var endPoint in _connection.GetEndPoints())
            {
                if (_connection.GetServer(endPoint).Version < ServerVersionWithExtendedSetCommand)
                {
                    return;
                }
            }
        }

        private void TryRegisterProfiler()
        {
            _ = _connection ?? throw new InvalidOperationException($"{nameof(_connection)} cannot be null.");

            if (_options.ProfilingSession != null)
            {
                _connection.RegisterProfiler(_options.ProfilingSession);
            }
        }

        private void CheckDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
        }


        public async Task<bool> SetHashAsync<T>(string hashId, string key, T t)
        {
            try
            {
                return await _cache.HashSetAsync(NewKey(key), hashId, JsonConvert.SerializeObject(t));
            }
            catch (Exception e)
            {
                _logger.LogError($"Redis Exception -> {e.Message}");
            }
            return false;
        }

        public async Task<bool> HashRemoveAsync(string hashId, string key)
        {
            try
            {
                return await _cache.HashDeleteAsync(NewKey(key), hashId);
            }
            catch (Exception e)
            {
                _logger.LogError($"Redis Exception -> {e.Message}");
            }
            return false;
        }

        public async Task<bool> KeyExpireAsync(string key, TimeSpan ttl)
        {
            try
            {
                return await _cache.KeyExpireAsync(NewKey(key), ttl);
            } catch (Exception e)
            {
                _logger.LogError($"Redis Exception -> {e.Message}");
            }
            return false;
        }

        public async Task<List<T>> GetHashAllAsync<T>(string key) where T : class
        {
            var list = new List<T>();
            try
            {
                var result = await _cache.HashGetAllAsync(NewKey(key));
                if (result.Any())
                {
                    foreach (var item in result)
                    {
                        var value = JsonConvert.DeserializeObject<T>(item.Value);
                        list.Add(value);
                    }
                }
            } catch(Exception e)
            {
                _logger.LogError($"Redis Exception -> {e.Message}");
            }
            return list;
        }

        public async Task<List<string>> GetHashAllStrAsync<T>(string key)
        {
            var list = new List<string>();
            try
            {
                var result = await _cache.HashGetAllAsync(NewKey(key));
                if (result.Any())
                {
                    foreach (var item in result)
                    {
                        list.Add(item.Value);
                    }
                }
            } catch(Exception e)
            {
                _logger.LogError($"Redis Exception -> {e.Message}");
            }
            return list;
        }

        public async Task<List<string>> GetHashKeysAsync(string key)
        {
            var list = new List<string>();
            try
            {
                var result = await _cache.HashKeysAsync(NewKey(key));
                if (result.Any())
                {
                    foreach (var item in result)
                    {
                        list.Add(item);
                    }
                }
            } catch (Exception e)
            {
                _logger.LogError($"Redis Exception -> {e.Message}");
            }
            return list;
        }

        public async Task<T> GetHashAsync<T>(string key, string hashField) where T : class
        {
            try
            {
                var result = await _cache.HashGetAsync(NewKey(key), hashField);
                if (result.HasValue)
                {
                    return JsonConvert.DeserializeObject<T>(result);
                }
            } catch(Exception e)
            {
                _logger.LogError($"Redis Exception -> {e.Message}");
            }
            return null;
        }

        public async Task<T> GetHash1Async<T>(string key, string hashfield) where T : class, new()
        {
            try
            {
                return await GetHashAsync<T>(key, hashfield) ?? new T();
            } catch (Exception e)
            {
                _logger.LogError($"Redis Exception -> {e.Message}");
                return null;
            }
        }

        private string NewKey(string key)
        {
            return $"{_instance}{key}";
        }

        public Task<long> Increment(string hashId, string key)
        {
            return _cache.HashIncrementAsync(NewKey(key), hashId);
        }

        public Task<bool> KeyExistsAsync(string key)
        {
            return _cache.KeyExistsAsync(key);
        }

        public async Task<bool> KeySetAsync<T>(string key, T value) where T : class, new()
        {
            try
            {
                return await _cache.SetAddAsync(NewKey(key), JsonConvert.SerializeObject(value));
            }
            catch (Exception e)
            {
                _logger.LogError($"Redis Exception -> {e.Message}");
            }
            return false;
        }

        public async Task<T> KeyGetAsync<T>(string key) where T : class
        {
            try
            {
                var result = await _cache.StringGetAsync(NewKey(key));
                if (result.HasValue)
                {
                    return JsonConvert.DeserializeObject<T>(result);
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"Redis Exception -> {e.Message}");
            }
            return null;
        }
    }
}
