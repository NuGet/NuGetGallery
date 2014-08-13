using Newtonsoft.Json.Linq;
using NuGet3.Client.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatalogTests
{
    public class JsonLdCacheTests
    {
        public async static Task Test0Async()
        {
            Uri uri = new Uri("http://localhost:8000/MyPackage.json");

            JsonLdPageCache cache = new JsonLdPageCache();

            JToken packageRegistration = await cache.Fetch(uri);

            foreach (JToken item in packageRegistration["package"])
            {
                JToken package = await cache.FetchArrayItem(item);
                JToken details = await cache.FetchProperty(package, "details");

                Console.WriteLine(details["description"]);
            }
        }

        public static void Test0()
        {
            Test0Async().Wait();
        }
    }
}
