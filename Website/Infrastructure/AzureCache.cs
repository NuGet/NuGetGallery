using Microsoft.ApplicationServer.Caching;

namespace NuGetGallery
{
    public class AzureCache : ICache
    {
        DataCache _dataCache;
        
        public AzureCache(DataCache dataCache)
        {
            _dataCache = dataCache;
        }
        
        public T Get<T>(string key)
        {
            return (T)_dataCache.Get(key);
        }

        public void Remove(string key)
        {
            _dataCache.Remove(key);
        }

        public void Set(string key, object value)
        {
            // DataCache doesn't allow null values, so we remove instead in that case.
            if (value == null)
                _dataCache.Remove(key);
            else
                _dataCache.Put(
                    key,
                    value);
        }
    }
}