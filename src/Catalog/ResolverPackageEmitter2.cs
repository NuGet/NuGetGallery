using Catalog.Persistence;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using VDS.RDF;
using VDS.RDF.Query;

namespace Catalog
{
    //  This is an implementation of the ResolverPackageEmitter that uses more of the TPL dataflow
    //  abstractions. the code is cleaner but it runs marginally slower at the moment
    //  and also hits a limit about 30% of the way through a full run. Basically its hard to implement
    //  much in the way of push-back with dataflow - because its not really the point.

    //  It might be better to create [and complete] a dataflow pipeline to handle an entire batch.

    public class ResolverPackageEmitter2 : CountingPackageEmitter
    {
        int _resolverResourceCount = 0;
        int _mergedResourceCount = 0;

        Storage _storage;
        JObject _resolverFrame;
        TransformBlock<JObject, IGraph> _createGraph = null;
        ActionBlock<TripleStore> _processGraph = null;

        public ResolverPackageEmitter2(Storage storage, int maxBatchSize = 1000)
        {
            Options.InternUris = false;

            _storage = storage;

            _resolverFrame = JObject.Parse(Utils.GetResource("context.ResolverFrame.json"));

            _processGraph = new ActionBlock<TripleStore>(async (tripleStore) =>
            {
                Console.WriteLine("received {0:N0} triple store", tripleStore.Triples.Count());

                await Process(tripleStore);
            },
            new ExecutionDataflowBlockOptions
            { 
                MaxDegreeOfParallelism = 1,         //  we currently do not lock the storage blobs
                BoundedCapacity = 1
            });

            _createGraph = new TransformBlock<JObject, IGraph>((doc) => Utils.CreateGraph(doc));

            BatchBlock<IGraph> batchGraphs = new BatchBlock<IGraph>(maxBatchSize);
            
            TransformBlock<IEnumerable<IGraph>, TripleStore> loadGraph = new TransformBlock<IEnumerable<IGraph>, TripleStore>((graphs) =>
            {
                TripleStore store = new TripleStore();
                foreach (IGraph graph in graphs)
                {
                    store.Add(graph, true);
                }
                return store;
            });

            _createGraph.LinkTo(batchGraphs, new DataflowLinkOptions { PropagateCompletion = true });

            batchGraphs.LinkTo(loadGraph, new DataflowLinkOptions { PropagateCompletion = true });
            
            loadGraph.LinkTo(_processGraph, new DataflowLinkOptions { PropagateCompletion = true });
        }

        protected override async Task EmitPackage(JObject package)
        {
            await base.EmitPackage(package);
            bool success = await _createGraph.SendAsync(package);
            if (!success)
            {
                throw new Exception("_createGraph.SendAsync failed");
            }
        }

        public override async Task Close()
        {
            _createGraph.Complete();

            await _processGraph.Completion;

            await base.Close();
        }

        async Task Process(TripleStore store)
        {
            try
            {
                //Console.WriteLine("process {0:N0} triples (memory: {1:N0} bytes)", store.Triples.Count(), GC.GetTotalMemory(true));

                SparqlResultSet distinctIds = SparqlHelpers.Select(store, Utils.GetResource("sparql.SelectDistinctPackage.rq"));

                IDictionary<Uri, IGraph> resolverResources = new Dictionary<Uri, IGraph>();

                foreach (SparqlResult row in distinctIds)
                {
                    string id = row["id"].ToString();

                    SparqlParameterizedString sparql = new SparqlParameterizedString();
                    sparql.CommandText = Utils.GetResource("sparql.ConstructResolverGraph.rq");

                    string baseAddress = _storage.BaseAddress + _storage.Container + "/resolver/";

                    sparql.SetLiteral("id", id);
                    sparql.SetLiteral("base", baseAddress);
                    sparql.SetLiteral("extension", ".json");

                    IGraph packageRegistration = SparqlHelpers.Construct(store, sparql.ToString());

                    Uri registrationUri = new Uri(baseAddress + id.ToLowerInvariant() + ".json");

                    resolverResources.Add(registrationUri, packageRegistration);
                }

                if (resolverResources.Count != distinctIds.Count)
                {
                    Console.WriteLine("\t{0} {1}", resolverResources.Count, distinctIds.Count);
                    throw new Exception("resolverResources.Count != distinctIds.Count");
                }

                await MergeAll(resolverResources);

                Interlocked.Add(ref _resolverResourceCount, resolverResources.Count);

                //Console.WriteLine(" [created {0} merged {1}]", _resolverResourceCount, _mergedResourceCount);
            }
            finally
            {
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
            string existingJson = await _storage.Load(resource.Key);
            if (existingJson != null)
            {
                IGraph existingGraph = Utils.CreateGraph(existingJson);
                resource.Value.Merge(existingGraph);

                Interlocked.Increment(ref _mergedResourceCount);
            }

            string content = Utils.CreateJson(resource.Value, _resolverFrame);
            await _storage.Save("application/json", resource.Key, content);
        }
    }
}
