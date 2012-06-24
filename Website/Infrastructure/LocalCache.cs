using System.Collections.Generic;

namespace NuGetGallery
{
    public class LocalCache : ICache
    {
        Dictionary<string, object> _cache;

        public LocalCache()
        {
            _cache = new Dictionary<string, object>();
        }

        public T Get<T>(string key)
        {
            if (_cache.ContainsKey(key))
                return (T)_cache[key];

            return default(T);
        }

        public void Remove(string key)
        {
            if (_cache.ContainsKey(key))
                _cache.Remove(key);
        }

        public void Set(
            string key,
            object value)
        {
            _cache[key] = value;
        }
    }
}