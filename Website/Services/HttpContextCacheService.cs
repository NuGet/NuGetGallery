using System;
using System.Web;
using System.Web.Caching;

namespace NuGetGallery
{
    public class HttpContextCacheService : ICacheService
    {
        private readonly HttpContextBase _httpContext;

        public HttpContextCacheService()
        {
            if (HttpContext.Current == null)
            {
                throw new InvalidOperationException("An HttpContext object is not available.");
            }
            _httpContext = new HttpContextWrapper(HttpContext.Current);
        }

        public object GetItem(string key)
        {
            return _httpContext.Cache.Get(key);
        }

        public void SetItem(string key, object item, TimeSpan timeout)
        {
            _httpContext.Cache.Remove(key);
            _httpContext.Cache.Insert(
                key, item, dependencies: null, absoluteExpiration: Cache.NoAbsoluteExpiration, slidingExpiration: timeout);
        }

        public void RemoveItem(string key)
        {
            _httpContext.Cache.Remove(key);
        }
    }
}