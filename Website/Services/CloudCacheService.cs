using System;
using System.Security;
using System.Text.RegularExpressions;
using Microsoft.ApplicationServer.Caching;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class CloudCacheService : ICacheService
    {
        // The DataCacheFactory object is expensive.
        // It should be created only once per app domain.
        private readonly DataCacheFactory _cacheFactory;

        public CloudCacheService(IAppConfiguration configuration)
        {
            var connection = new AzureCacheConnectionString(configuration.AzureCacheConnectionString);

            DataCacheFactoryConfiguration cacheConfig = new DataCacheFactoryConfiguration();
            cacheConfig.Servers = new[] {
                new DataCacheServerEndpoint(connection.Endpoint, 22233)
            };
            SecureString key = connection.Key.ToSecureString();
            DataCacheSecurity security = new DataCacheSecurity(key);
            cacheConfig.SecurityProperties = security;

            _cacheFactory = new DataCacheFactory(cacheConfig);
        }

        public object GetItem(string key)
        {
            DataCache dataCache = _cacheFactory.GetDefaultCache();
            return dataCache.Get(key);
        }

        public void SetItem(string key, object item, TimeSpan timeout)
        {
            DataCache dataCache = _cacheFactory.GetDefaultCache();
            dataCache.Put(key, item, timeout);
        }

        public void RemoveItem(string key)
        {
            DataCache dataCache = _cacheFactory.GetDefaultCache();
            dataCache.Remove(key);
        }

        private class AzureCacheConnectionString
        {
            private static readonly Regex Parser = new Regex("^Endpoint=(?<endpoint>[^;]*);Key=(?<key>[^;]*);?$");

            public string Endpoint { get; set; }
            public string Key { get; set; }

            public AzureCacheConnectionString(string str)
            {
                var m = Parser.Match(str);
                if (!m.Success)
                {
                    throw new FormatException("Invalid Azure Cache Connection String. Expected 'Endpoint=<url>;Key=<key>;'. Actual: " + str);
                }
                Endpoint = m.Groups["endpoint"].Value;
                Key = m.Groups["key"].Value;
            }
        }
    }
}