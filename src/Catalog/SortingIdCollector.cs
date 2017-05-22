using System;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class SortingIdCollector : SortingCollector<string>
    {
        public SortingIdCollector(Uri index, Func<HttpMessageHandler> handlerFunc = null) : base(index, handlerFunc)
        {
        }

        protected override string GetKey(JObject item)
        {
            return item["nuget:id"].ToString();
        }
    }
}
