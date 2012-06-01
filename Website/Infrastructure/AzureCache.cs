using System;
using Microsoft.ApplicationServer.Caching;

namespace NuGetGallery.Infrastructure
{
	public class AzureCache : ICache
	{
		private readonly DataCache _dataCache;
		
		public AzureCache(DataCache dataCache)
		{
			if (dataCache == null)
				throw new ArgumentNullException("dataCache");

			_dataCache = dataCache;
		}
		
		public void Add(string key, object value)
		{
			_dataCache.Add(key, value);
		}

		public object Get(string key)
		{
			return _dataCache.Get(key);
		}

		public void Remove(string key)
		{
			_dataCache.Remove(key);
		}
	}
}