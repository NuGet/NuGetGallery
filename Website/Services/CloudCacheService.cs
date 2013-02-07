using System;
using System.Security;
using Microsoft.ApplicationServer.Caching;

namespace NuGetGallery
{
    public class CloudCacheService : ICacheService
    {
        // The DataCacheFactory object is expensive.
        // It should be created only once per app domain.
        private readonly DataCacheFactory _cacheFactory;

        public CloudCacheService(IConfiguration configuration)
        {
            DataCacheFactoryConfiguration cacheConfig = new DataCacheFactoryConfiguration();
            cacheConfig.Servers = new[] {
                new DataCacheServerEndpoint(configuration.AzureCacheEndpoint, 22233)
            };
            SecureString key = configuration.AzureCacheKey.ToSecureString();
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
    }
}