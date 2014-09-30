using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Maintenance;
using VDS.RDF;
using NuGet.Services.Metadata.Catalog.Collecting;
using NuGet.Services.Metadata.Catalog;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public class RegistrationCatalogCollector : SortingGraphCollector
    {
        StorageFactory _storageFactory;

        public RegistrationCatalogCollector(StorageFactory storageFactory, int batchSize)
            : base(batchSize, new Uri[] { Schema.DataTypes.Package })
        {
            _storageFactory = storageFactory;

            ContentBaseAddress = "http://tempuri.org";

            PartitionSize = 40;

            PackageCountThreshold = 150;
        }

        public string ContentBaseAddress { get; set; }
        public int PartitionSize { get; set; }
        public int PackageCountThreshold { get; set; }

        protected override async Task ProcessGraphs(KeyValuePair<string, IDictionary<string, IGraph>> sortedGraphs)
        {
            Storage storage = _storageFactory.Create(sortedGraphs.Key);

            int count = await GetPackageCount(storage);

            int total = count + sortedGraphs.Value.Count;

            if (total < PackageCountThreshold)
            {
                await SaveSmallRegistration(storage, sortedGraphs);
            }
            else
            {
                await SaveLargeRegistration(storage, sortedGraphs);
            }
        }

        async Task<int> GetPackageCount(Storage storage)
        {
            Uri resourceUri = storage.ResolveUri("index.json");

            string json = await storage.LoadString(resourceUri);

            if (json == null)
            {
                return 0;
            }

            JObject index = JObject.Parse(json);

            int total = 0;
            foreach (JObject item in index["items"])
            {
                total += item["count"].ToObject<int>();
            }

            return total;
        }

        async Task SaveSmallRegistration(Storage storage, KeyValuePair<string, IDictionary<string, IGraph>> sortedGraphs)
        {
            using (GraphRegistrationCatalogWriter writer = new GraphRegistrationCatalogWriter(storage))
            {
                writer.PartitionSize = PartitionSize;

                foreach (KeyValuePair<string, IGraph> item in sortedGraphs.Value)
                {
                    writer.Add(new RegistrationCatalogItem(item.Key, item.Value, _storageFactory.BaseAddress, ContentBaseAddress));
                }
                await writer.Commit(DateTime.UtcNow);

                // now the commit has happened the writer.Graph should contain all the data

                JObject frame = (new CatalogContext()).GetJsonLdContext("context.Registration.json", writer.TypeUri);
                StorageContent content = new StringStorageContent(Utils.CreateJson(writer.Graph, frame), "application/json", "no-store");
                await storage.Save(writer.ResourceUri, content);
            }
        }

        async Task SaveLargeRegistration(Storage storage, KeyValuePair<string, IDictionary<string, IGraph>> sortedGraphs)
        {
            IList<Uri> cleanUpList = new List<Uri>();

            using (RegistrationCatalogWriter writer = new RegistrationCatalogWriter(storage, cleanUpList))
            {
                writer.PartitionSize = PartitionSize;

                foreach (KeyValuePair<string, IGraph> item in sortedGraphs.Value)
                {
                    writer.Add(new RegistrationCatalogItem(item.Key, item.Value, _storageFactory.BaseAddress, ContentBaseAddress));
                }
                await writer.Commit(DateTime.UtcNow);
            }

            // because there were multiple files some might now be irrelevant

            foreach (Uri uri in cleanUpList)
            {
                Console.WriteLine("DELETE {0}", uri);
                await storage.Delete(uri);
            }
        }
    }
}
