using System;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class SortingIdVersionCollector : SortingCollector<FeedPackageIdentity>
    {
        public SortingIdVersionCollector(Uri index, Func<HttpMessageHandler> handlerFunc = null) : base(index, handlerFunc)
        {
        }

        protected override FeedPackageIdentity GetKey(JObject item)
        {
            return new FeedPackageIdentity(item["nuget:id"].ToString(), item["nuget:version"].ToString());
        }
    }
}
