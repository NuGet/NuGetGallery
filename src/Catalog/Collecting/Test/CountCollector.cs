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

        protected override async Task Fetch(CollectorHttpClient client, Uri index, DateTime last)
        {
            JObject root = await client.GetJObjectAsync(index);

            List<Task<JObject>> tasks = new List<Task<JObject>>();

            foreach (JObject rootItem in root["item"])
            {
                DateTime pageTimeStamp = rootItem["timeStamp"]["@value"].ToObject<DateTime>();

                if (pageTimeStamp > last)
                {
                    int count = int.Parse(rootItem["count"].ToString());

                    Total += count;
                }
            }
        }
    }
}
