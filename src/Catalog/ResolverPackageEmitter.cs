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
    public class ResolverPackageEmitter : CountingPackageEmitter
    {
        int _resolverResourceCount = 0;
        int _mergedResourceCount = 0;

        Storage _storage;
        int _currentBatchSize = 0;
        int _maxBatchSize;
        TripleStore _currentStore;
        ActionBlock<TripleStore> _actionBlock = null;

        public ResolverPackageEmitter(Storage storage, int maxBatchSize = 1000)
        {
            _storage = storage;
            _maxBatchSize = maxBatchSize;

            _actionBlock = new ActionBlock<TripleStore>(async (tripleStore) =>
            {
                await Process(tripleStore);
            },
            new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });  //  just to protect storage
        }

        protected override void EmitPackage(JObject package)
        {
            base.EmitPackage(package);

            IGraph graph = Utils.CreateGraph(package);

            lock (this)
            {
                if (_currentStore == null)
                {
                    _currentStore = new TripleStore();
                }

                _currentStore.Add(graph, true);

                if (_currentBatchSize++ == _maxBatchSize)
                {
                    _actionBlock.Post(_currentStore);
                    _currentBatchSize = 0;
                    _currentStore = null;
                }
            }
        }

        public override void Close()
        {
            lock (this)
            {
                if (_currentBatchSize > 0)
                {
                    _actionBlock.Post(_currentStore);
                }
            }

            _actionBlock.Complete();
            _actionBlock.Completion.Wait();

            Console.WriteLine("created {0} resolver resources", _resolverResourceCount);
            Console.WriteLine("merged {0} resolver resources", _mergedResourceCount);

            base.Close();
        }

        async Task Process(TripleStore store)
        {
            Console.WriteLine("found {0} triples", store.Triples.Count());

            SparqlResultSet distinctIds = SparqlHelpers.Select(store, Utils.GetResource("sparql.SelectDistinctPackage.rq"));

            IDictionary<Uri, IGraph> resolverResources = new Dictionary<Uri, IGraph>();

            foreach (SparqlResult row in distinctIds)
            {
                string id = row["id"].ToString();

                SparqlParameterizedString sparql = new SparqlParameterizedString();
                sparql.CommandText = Utils.GetResource("sparql.ConstructResolverGraph.rq");

                string baseAddress = _storage.BaseAddress + _storage.Container + "/";

                sparql.SetLiteral("id", id);
                sparql.SetLiteral("base", baseAddress);
                sparql.SetLiteral("extension", ".json");

                IGraph packageRegistration = SparqlHelpers.Construct(store, sparql.ToString());

                Uri registrationUri = new Uri(baseAddress + id.ToLowerInvariant() + ".json");

                resolverResources.Add(registrationUri, packageRegistration);
            }

            await Merge(resolverResources);

            Interlocked.Add(ref _resolverResourceCount, resolverResources.Count);
        }

        async Task Merge(IDictionary<Uri, IGraph> resolverResources)
        {
            JObject resolverFrame = JObject.Parse(Utils.GetResource("context.ResolverFrame.json"));

            foreach (KeyValuePair<Uri, IGraph> resolverResource in resolverResources)
            {
                Uri resourceUri = resolverResource.Key;
                IGraph newGraph = resolverResource.Value;

                string existingJson = await _storage.Load(resourceUri);
                if (existingJson != null)
                {
                    IGraph existingGraph = Utils.CreateGraph(existingJson);
                    newGraph.Merge(existingGraph);

                    Interlocked.Increment(ref _mergedResourceCount);
                }

                string content = Utils.CreateJson(newGraph, resolverFrame);
                await _storage.Save("application/json", resourceUri, content);
            }
        }
    }
}
