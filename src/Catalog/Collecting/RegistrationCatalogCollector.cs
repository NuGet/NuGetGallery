using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VDS.RDF;

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

            PartitionSize = 32;
            PackageCountThreshold = 128;
        }

        public string ContentBaseAddress { get; set; }
        public int PartitionSize { get; set; }
        public int PackageCountThreshold { get; set; }

        protected override async Task ProcessGraphs(KeyValuePair<string, IDictionary<string, IGraph>> sortedGraphs)
        {
            Storage storage = _storageFactory.Create(sortedGraphs.Key);

            int count = await Collector.GetItemCount(storage);

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

        async Task SaveSmallRegistration(Storage storage, KeyValuePair<string, IDictionary<string, IGraph>> sortedGraphs)
        {
            SingleGraphPersistence graphPersistence = new SingleGraphPersistence(storage.BaseAddress);

            await SaveRegistration(storage, sortedGraphs, null, graphPersistence);

            // now the commit has happened the graphPersistence.Graph should contain all the data

            JObject frame = (new CatalogContext()).GetJsonLdContext("context.Registration.json", graphPersistence.TypeUri);
            StorageContent content = new StringStorageContent(Utils.CreateJson(graphPersistence.Graph, frame), "application/json", "no-store");
            await storage.Save(graphPersistence.ResourceUri, content);
        }

        async Task SaveLargeRegistration(Storage storage, KeyValuePair<string, IDictionary<string, IGraph>> sortedGraphs)
        {
            IList<Uri> cleanUpList = new List<Uri>();

            await SaveRegistration(storage, sortedGraphs, cleanUpList, null);

            // because there were multiple files some might now be irrelevant

            foreach (Uri uri in cleanUpList)
            {
                await storage.Delete(uri);
            }
        }

        async Task SaveRegistration(Storage storage, KeyValuePair<string, IDictionary<string, IGraph>> sortedGraphs, IList<Uri> cleanUpList, SingleGraphPersistence graphPersistence)
        {
            using (RegistrationCatalogWriter writer = new RegistrationCatalogWriter(storage, PartitionSize, cleanUpList, graphPersistence))
            {
                foreach (KeyValuePair<string, IGraph> item in sortedGraphs.Value)
                {
                    writer.Add(new RegistrationCatalogItem(item.Key, item.Value, _storageFactory.BaseAddress, ContentBaseAddress));
                }
                await writer.Commit(DateTime.UtcNow);
            }
        }
    }
}
