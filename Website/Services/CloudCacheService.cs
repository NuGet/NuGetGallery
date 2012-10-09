using System;
using Microsoft.ApplicationServer.Caching;

namespace NuGetGallery
{
	public class CloudCacheService : ICacheService
	{
		// The DataCacheFactory object is expensive.
		// It should be created only once per app domain.
		private static readonly DataCacheFactory _cacheFactory = new DataCacheFactory();

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
	}
}