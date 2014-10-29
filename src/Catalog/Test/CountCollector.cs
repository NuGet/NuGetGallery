using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Test
{
    public class CountCollector : CollectorBase
    {
        public CountCollector(Uri index, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, handlerFunc)
        {
            Total = 0;
        }

        public int Total
        {
            get;
            private set;
        }

        protected override async Task<bool> Fetch(CollectorHttpClient client, ReadWriteCursor front, ReadCursor back)
        {
            await front.Load();

            DateTime frontDateTime = front.Value;

            JObject root = await client.GetJObjectAsync(Index);

            List<Task<JObject>> tasks = new List<Task<JObject>>();

            foreach (JObject rootItem in root["items"])
            {
                DateTime pageTimeStamp = rootItem["commitTimeStamp"].ToObject<DateTime>();

                if (pageTimeStamp > frontDateTime)
                {
                    int count = int.Parse(rootItem["count"].ToString());

                    Total += count;

                    front.Value = pageTimeStamp;
                    await front.Save();
                }
            }

            return true;
        }
    }
}
