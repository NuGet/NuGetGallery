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
        }

        public string ContentBaseAddress { get; set; }

        protected override async Task ProcessGraphs(KeyValuePair<string, IDictionary<string, IGraph>> sortedGraphs)
        {
            Storage storage = _storageFactory.Create(sortedGraphs.Key);

            IList<Uri> cleanUpList = new List<Uri>();          

            RegistrationCatalogWriter writer = new RegistrationCatalogWriter(storage, cleanUpList);
            foreach (KeyValuePair<string, IGraph> item in sortedGraphs.Value)
            {
                writer.Add(new RegistrationCatalogItem(item.Key, item.Value, _storageFactory.BaseAddress, ContentBaseAddress));
            }
            await writer.Commit(DateTime.UtcNow);

            foreach (Uri uri in cleanUpList)
            {
                Console.WriteLine("DELETE {0}", uri);
                await storage.Delete(uri);
            }
        }
    }
}
