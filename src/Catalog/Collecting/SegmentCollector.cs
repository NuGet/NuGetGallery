using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Segmentation;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public class SegmentCollector : BatchCollector
    {
        Uri[] _types;
        Storage _storage;
        string _registrationBaseAddress;

        public SegmentCollector(int batchSize, Storage storage, string registrationBaseAddress)
            : base(batchSize)
        {
            _types = new Uri[] { Schema.DataTypes.Package };

            _storage = storage;
            _registrationBaseAddress = registrationBaseAddress;
        }

        protected override async Task<bool> ProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
        {
            List<Task<JObject>> tasks = new List<Task<JObject>>();

            foreach (JObject item in items)
            {
                if (Utils.IsType(context, item, _types))
                {
                    Uri itemUri = item["url"].ToObject<Uri>();
                    tasks.Add(client.GetJObjectAsync(itemUri));
                }
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks.ToArray());

                SegmentWriter writer = new SegmentWriter(_storage, "allversions", 1000, true);

                foreach (Task<JObject> task in tasks)
                {
                    JObject package = task.Result;

                    string id = package["id"].ToString();
                    string version = package["version"].ToString();
                    string description = package["description"].ToString();

                    writer.Add(new IdVersionKeyEntry(id, version, description, _registrationBaseAddress));
                }

                await writer.Commit();
            }

            return true;
        }
    }
}
