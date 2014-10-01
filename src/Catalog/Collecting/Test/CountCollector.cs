using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Collecting.Test
{
    public class CountCollector : Collector
    {
        public CountCollector()
        {
            Total = 0;
        }

        public int Total
        {
            get;
            private set;
        }

        protected override async Task<CollectorCursor> Fetch(CollectorHttpClient client, Uri index, CollectorCursor last)
        {
            CollectorCursor cursor = last;
            DateTime lastDateTime = (DateTime)cursor;

            JObject root = await client.GetJObjectAsync(index);

            List<Task<JObject>> tasks = new List<Task<JObject>>();

            foreach (JObject rootItem in root["items"])
            {
                DateTime pageTimeStamp = rootItem["commitTimeStamp"].ToObject<DateTime>();

                if (pageTimeStamp > lastDateTime)
                {
                    cursor = pageTimeStamp;

                    int count = int.Parse(rootItem["count"].ToString());

                    Total += count;
                }
            }

            return cursor;
        }
    }
}
