using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
	public class CloudCacheService : ICacheService
	{
		public object GetItem(string key)
		{
			throw new NotImplementedException();
		}

		public void SetItem(string key, object item, TimeSpan timeout)
		{
			throw new NotImplementedException();
		}
	}
}