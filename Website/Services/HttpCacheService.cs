using System;
using System.Web;
using System.Web.Caching;

namespace NuGetGallery
{
    public class HttpCacheService : ICacheService
    {
        public object GetItem(string key)
        {
            if (HttpContext.Current == null)
            {
                throw new InvalidOperationException("An HttpContext object is not available.");
            }
            return HttpContext.Current.Cache.Get(key);
        }

        public void SetItem(string key, object item, TimeSpan timeout)
        {
            if (HttpContext.Current == null)
            {
                throw new InvalidOperationException("An HttpContext object is not available.");
            }

            HttpContext.Current.Cache.Remove(key);
            HttpContext.Current.Cache.Insert(
                key, item, dependencies: null, absoluteExpiration: Cache.NoAbsoluteExpiration, slidingExpiration: timeout);
        }
    }
}