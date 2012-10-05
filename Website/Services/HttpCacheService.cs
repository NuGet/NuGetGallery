using System;
using System.Web;
using System.Web.Caching;

namespace NuGetGallery
{
	public class HttpCacheService : ICacheService
	{
		public HttpCacheService()
		{
		}

		public object GetItem(string key)
		{
			return HttpContext.Current.Cache.Get(key);
		}

		public void SetItem(string key, object item, TimeSpan timeout)
		{
			HttpContext.Current.Cache.Remove(key);
			HttpContext.Current.Cache.Insert(
				key, item, dependencies: null, absoluteExpiration: Cache.NoAbsoluteExpiration, slidingExpiration: timeout);
		}
	}
}