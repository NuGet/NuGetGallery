using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Query;
using System.Diagnostics.Tracing;
using System.Text;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public class ResolverCollector : StoreCollector
    {
        Storage _storage;
        JObject _resolverFrame;

        public string GalleryBaseAddress { get; set; }
        public string CdnBaseAddress { get; set; }

        public ResolverCollector(Storage storage, int batchSize)
            : base(batchSize, new Uri[] { Schema.DataTypes.Package })
        {
            _resolverFrame = JObject.Parse(Utils.GetResource("context.Resolver.json"));
            _resolverFrame["@type"] = "PackageRegistration";
            _storage = storage;
        }

        protected override async Task ProcessStore(TripleStore store)
        {
            ResolverCollectorEventSource.Log.ProcessingBatch(BatchCount);
            try
            {
                SparqlResultSet distinctIds = SparqlHelpers.Select(store, Utils.GetResource("sparql.SelectDistinctPackage.rq"));

                IDictionary<Uri, IGraph> resolverResources = new Dictionary<Uri, IGraph>();

                foreach (SparqlResult row in distinctIds)
                {
                    string id = row["id"].ToString();

                    SparqlParameterizedString sparql = new SparqlParameterizedString();
                    sparql.CommandText = Utils.GetResource("sparql.ConstructResolverGraph.rq");

                    string baseAddress = _storage.BaseAddress.ToString();

                    sparql.SetLiteral("id", id);
                    sparql.SetLiteral("base", baseAddress);
                    sparql.SetLiteral("extension", ".json");
                    sparql.SetLiteral("galleryBase", GalleryBaseAddress);
                    sparql.SetLiteral("cdnBase", CdnBaseAddress);

                    IGraph packageRegistration = SparqlHelpers.Construct(store, sparql.ToString());

                    Uri registrationUri = new Uri(baseAddress + id.ToLowerInvariant() + ".json");
                    resolverResources.Add(registrationUri, packageRegistration);
                }

                if (resolverResources.Count != distinctIds.Count)
                {
                    throw new Exception("resolverResources.Count != distinctIds.Count");
                }

                await MergeAll(resolverResources);
            }
            finally
            {
                ResolverCollectorEventSource.Log.ProcessedBatch(BatchCount);
                store.Dispose();
            }
        }

        async Task MergeAll(IDictionary<Uri, IGraph> resolverResources)
        {
            List<Task> tasks = new List<Task>();
            foreach (KeyValuePair<Uri, IGraph> resolverResource in resolverResources)
            {
                tasks.Add(Task.Run(async () => { await Merge(resolverResource); }));
            }
            await Task.WhenAll(tasks.ToArray());
        }

        async Task Merge(KeyValuePair<Uri, IGraph> resource)
        {
            string existingJson;
            ResolverCollectorEventSource.Log.LoadingBlob(resource.Key.ToString());
            try {
                existingJson = await _storage.LoadString(resource.Key);
            } catch(Exception ex) {
                ResolverCollectorEventSource.Log.ErrorLoadingBlob(resource.Key.ToString(), ex.ToString());
                throw;
            }
            ResolverCollectorEventSource.Log.LoadedBlob(resource.Key.ToString());
            
            if (existingJson != null)
            {
                IGraph existingGraph = Utils.CreateGraph(existingJson);
                resource.Value.Merge(existingGraph);
            }

            string json = Utils.CreateJson(resource.Value, _resolverFrame);
            StorageContent content = new StringStorageContent(
                json, 
                contentType: "application/json", 
                cacheControl: "public, max-age=300, s-maxage=300");

            // Estimate the file size and report it
            ResolverCollectorEventSource.Log.EmittingBlob(resource.Key.ToString(), Encoding.UTF8.GetByteCount(json) / 1024);
            try
            {
                await _storage.Save(resource.Key, content);
            } catch(Exception ex) {
                ResolverCollectorEventSource.Log.ErrorEmittingBlob(resource.Key.ToString(), ex.ToString());
                throw;
            }
            ResolverCollectorEventSource.Log.EmittedBlob(resource.Key.ToString());
        }
    }

    public class ResolverCollectorEventSource : EventSource {
        public static readonly ResolverCollectorEventSource Log = new ResolverCollectorEventSource();
        private ResolverCollectorEventSource() {}

        [Event(
            eventId: 1,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Start,
            Task = Tasks.ProcessingBatch,
            Message = "Processing batch #{0}")]
        public void ProcessingBatch(int batchNumber) { WriteEvent(1, batchNumber); }

        [Event(
            eventId: 2,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Task = Tasks.ProcessingBatch,
            Message = "Processed batch #{0}")]
        public void ProcessedBatch(int batchNumber) { WriteEvent(2, batchNumber); }

        [Event(
            eventId: 3,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Start,
            Task = Tasks.LoadingBlob,
            Message = "Loading existing blob: {0}")]
        public void LoadingBlob(string blob) { WriteEvent(3, blob); }

        [Event(
            eventId: 4,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Task = Tasks.LoadingBlob,
            Message = "Loaded blob: {0}")]
        public void LoadedBlob(string blob) { WriteEvent(4, blob); }

        [Event(
            eventId: 5,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Start,
            Task = Tasks.EmittingBlob,
            Message = "Emitting blob: {0} (~{1:0.00}KB)")]
        public void EmittingBlob(string blob, double sizeInKB) { WriteEvent(5, blob, sizeInKB); }

        [Event(
            eventId: 6,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Task = Tasks.EmittingBlob,
            Message = "Emitted blob: {0}")]
        public void EmittedBlob(string blob) { WriteEvent(6, blob); }

        [Event(
            eventId: 7,
            Level = EventLevel.Error,
            Opcode = EventOpcode.Stop,
            Task = Tasks.EmittingBlob,
            Message = "Error emitting blob '{0}': {1}")]
        public void ErrorEmittingBlob(string blob, string exception) { WriteEvent(7, blob, exception); }

        [Event(
            eventId: 8,
            Level = EventLevel.Error,
            Opcode = EventOpcode.Stop,
            Task = Tasks.LoadingBlob,
            Message = "Error loading blob '{0}': {1}")]
        public void ErrorLoadingBlob(string blob, string exception) { WriteEvent(8, blob, exception); }

        public static class Tasks {
            public const EventTask ProcessingBatch = (EventTask)0x1;
            public const EventTask LoadingBlob = (EventTask)0x2;
            public const EventTask EmittingBlob = (EventTask)0x3;
        }
    }
}
