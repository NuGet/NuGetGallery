using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    public class IndexingHelpers
    {
        public static async Task<IDictionary<string, IList<JObject>>> GetPackages(Uri indexUri, bool verbose = false)
        {
            IDictionary<string, IList<JObject>> packages = new Dictionary<string, IList<JObject>>();

            HttpClient client = new HttpClient();

            if (verbose)
            {
                Trace.WriteLine(indexUri);
            }

            HttpResponseMessage indexResponse = await client.GetAsync(indexUri);
            string indexJson = await indexResponse.Content.ReadAsStringAsync();
            JObject index = JObject.Parse(indexJson);

            List<Task<HttpResponseMessage>> tasks = new List<Task<HttpResponseMessage>>();

            foreach (JObject indexItem in index["items"])
            {
                Uri pageUri = indexItem["url"].ToObject<Uri>();

                if (verbose)
                {
                    Trace.WriteLine(pageUri);
                }

                tasks.Add(client.GetAsync(pageUri));
            }

            Task.WaitAll(tasks.ToArray());

            foreach (Task<HttpResponseMessage> task in tasks)
            {
                HttpResponseMessage pageResponse = task.Result;

                string pageJson = await pageResponse.Content.ReadAsStringAsync();
                JObject page = JObject.Parse(pageJson);

                foreach (JObject pageItem in page["items"])
                {
                    string id = pageItem["nuget:id"].ToString();
                    string version = pageItem["nuget:version"].ToString();

                    IList<JObject> items;
                    if (!packages.TryGetValue(id, out items))
                    {
                        items = new List<JObject>();
                        packages.Add(id, items);
                    }

                    items.Add(pageItem);
                }
            }

            return packages;
        }

        public static async Task CreateNewCatalog(Storage storage, IDictionary<string, IList<JObject>> packages)
        {
            IList<KeyValuePair<string, IList<JObject>>> batch = new List<KeyValuePair<string, IList<JObject>>>();

            IList<JObject> catalogPages = new List<JObject>();

            int packageCount = 0;

            foreach (KeyValuePair<string, IList<JObject>> item in packages)
            {
                batch.Add(item);

                packageCount += item.Value.Count;

                if (packageCount >= 100)
                {
                    packageCount = 0;

                    catalogPages.Add(MakeCatalogPage(batch));
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                catalogPages.Add(MakeCatalogPage(batch));
            }

            JObject catalogIndex = MakeCatalogIndex(storage, catalogPages);

            await Save(storage, catalogIndex, catalogPages);
        }

        static async Task Save(Storage storage, JObject catalogIndex, IEnumerable<JObject> catalogPages)
        {
            foreach (JObject catalogPage in catalogPages)
            {
                Uri pageUri = new Uri(catalogPage["url"].ToString());
                await storage.Save(pageUri, new StringStorageContent(catalogPage.ToString(), "application/json"));
            }

            Uri indexUri = new Uri(catalogIndex["url"].ToString());
            await storage.Save(indexUri, new StringStorageContent(catalogIndex.ToString(), "application/json"));
        }

        static JObject MakeCatalogIndex(Storage storage, IList<JObject> pages)
        {
            JToken context;
            using (JsonReader jsonReader = new JsonTextReader(new StreamReader(Utils.GetResourceStream("context.Container.json"))))
            {
                JObject obj = JObject.Load(jsonReader);
                context = obj["@context"];
            }

            JObject newIndex = new JObject();

            newIndex["@type"] = "CatalogIndex";

            Uri indexUri = storage.ResolveUri("index.json");
            newIndex["url"] = indexUri.ToString();

            DateTime indexTimestamp = DateTime.MaxValue;

            JArray items = new JArray();

            int pageNumber = 0;

            foreach (JObject page in pages)
            {
                Uri pageUri = storage.ResolveUri(string.Format("page{0}.json", pageNumber++));
                page["url"] = pageUri.ToString();
                page["parent"] = newIndex["url"];
                page["@context"] = context;

                JObject indexItem = new JObject();
                indexItem["url"] = page["url"];
                indexItem["commitTimestamp"] = page["commitTimestamp"];
                indexItem["@type"] = "CatalogPage";
                indexItem["count"] = page["items"].Count();
                items.Add(indexItem);

                DateTime itemDataTime = page["commitTimestamp"].ToObject<DateTime>();
                if (itemDataTime < indexTimestamp)
                {
                    indexTimestamp = itemDataTime;
                }
            }

            newIndex["items"] = items;
            newIndex["commitTimestamp"] = indexTimestamp.ToString("O");

            newIndex["@context"] = context;

            return newIndex;
        }

        static JObject MakeCatalogPage(IList<KeyValuePair<string, IList<JObject>>> batch)
        {
            JObject newPage = new JObject();

            newPage["@type"] = "CatalogPage";

            DateTime pageTimestamp = DateTime.MaxValue;

            JArray items = new JArray();

            foreach (KeyValuePair<string, IList<JObject>> entry in batch)
            {
                foreach (JObject item in entry.Value)
                {
                    items.Add(item);

                    DateTime itemDataTime = item["commitTimestamp"].ToObject<DateTime>();
                    if (itemDataTime < pageTimestamp)
                    {
                        pageTimestamp = itemDataTime;
                    }
                }
            }

            newPage["items"] = items;
            newPage["commitTimestamp"] = pageTimestamp.ToString("O");

            return newPage;
        }
    }
}
