using System.Collections.Generic;

namespace NuGetGallery.Infrastructure
{
	public class LocalCache : ICache
	{
		private readonly Dictionary<string, object> _cache = new Dictionary<string, object>();
		
		public void Add(string key, object value)
		{
			_cache[key] = value;
		}

		public object Get(string key)
		{
			object value;
			return _cache.TryGetValue(key, out value) ? value : null;
		}

		public void Remove(string key)
		{
			_cache.Remove(key);
		}
	}
}