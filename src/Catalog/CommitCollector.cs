using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class CommitCollector : CollectorBase
    {
        public CommitCollector(Uri index, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, handlerFunc)
        {
        }

        protected override async Task<bool> Fetch(CollectorHttpClient client, ReadWriteCursor front, ReadCursor back)
        {
            IList<JObject> items = new List<JObject>();

            JObject root = await client.GetJObjectAsync(Index);

            IEnumerable<CatalogItem> rootItems = root["items"]
                .Select(item => new CatalogItem(item))
                .Where(item => item.CommitTimeStamp > front.Value && item.CommitTimeStamp <= back.Value)
                .OrderBy(item => item.CommitTimeStamp);

            bool acceptNextBatch = false;

            foreach (CatalogItem rootItem in rootItems)
            {
                JObject page = await client.GetJObjectAsync(rootItem.Uri);

                JToken context = null;
                page.TryGetValue("@context", out context);

                var batches = page["items"]
                    .Select(item => new CatalogItem(item))
                    .Where(item => item.CommitTimeStamp > front.Value && item.CommitTimeStamp <= back.Value)
                    .GroupBy(item => item.CommitTimeStamp)
                    .OrderBy(group => group.Key);

                foreach (var batch in batches)
                {
                    acceptNextBatch = await OnProcessBatch(client, batch.Select(item => item.Value), context, batch.Key);

                    front.Value = batch.Key;
                    await front.Save();

                    Trace.TraceInformation("CommitCatalog.Fetch front.Save has value: {0}", front);

                    if (!acceptNextBatch)
                    {
                        break;
                    }
                }

                if (!acceptNextBatch)
                {
                    break;
                }
            }

            return acceptNextBatch;
        }

        protected abstract Task<bool> OnProcessBatch(CollectorHttpClient client, IEnumerable<JToken> items, JToken context, DateTime commitTimeStamp);

        class CatalogItem : IComparable
        {
            public CatalogItem(JToken jtoken)
            {
                CommitTimeStamp = jtoken["commitTimeStamp"].ToObject<DateTime>();
                Uri = jtoken["@id"].ToObject<Uri>();
                Value = jtoken;
            }

            public DateTime CommitTimeStamp { get; private set; }
            public Uri Uri { get; private set; }
            public JToken Value { get; private set; }

            public int CompareTo(object obj)
            {
                return CommitTimeStamp.CompareTo(((CatalogItem)obj).CommitTimeStamp);
            }
        }
    }
}