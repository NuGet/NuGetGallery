using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Collecting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.GalleryIntegration
{
    public class GalleryKeyCollector : Collector
    {
        HashSet<int> _keys;

        public GalleryKeyCollector(HashSet<int> keys)
        {
            _keys = keys;
        }

        protected override async Task<CollectorCursor> Fetch(CollectorHttpClient client, Uri index, CollectorCursor last)
        {
            CollectorCursor cursor = last;
            DateTime lastDateTime = (DateTime)cursor;

            JObject root = await client.GetJObjectAsync(index);

            JToken context = null;
            root.TryGetValue("@context", out context);

            IEnumerable<JToken> rootItems = root["item"].OrderBy(item => item["timeStamp"]["@value"].ToObject<DateTime>());

            foreach (JObject rootItem in rootItems)
            {
                DateTime pageTimeStamp = rootItem["timeStamp"]["@value"].ToObject<DateTime>();

                if (pageTimeStamp > lastDateTime)
                {
                    cursor = pageTimeStamp;

                    Uri pageUri = rootItem["url"].ToObject<Uri>();
                    JObject page = await client.GetJObjectAsync(pageUri);

                    IEnumerable<JToken> pageItems = page["item"].OrderBy(item => item["timeStamp"]["@value"].ToObject<DateTime>());

                    foreach (JToken pageItem in pageItems)
                    {
                        JObject item = (JObject)pageItem;

                        JToken key;
                        if (item.TryGetValue("http://nuget.org/gallery#key", out key))
                        {
                            _keys.Add(key.ToObject<int>());
                        }
                    }
                }
            }

            return cursor;
        }
    }
}
