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
    public class DistinctCountingTripleStorePackageEmitter : CountingPackageEmitter
    {
        int _currentBatchSize = 0;
        int _maxBatchSize;
        TripleStore _currentStore;
        ActionBlock<TripleStore> _actionBlock = null;

        HashSet<string> _packageIds = new HashSet<string>();

        public DistinctCountingTripleStorePackageEmitter(int maxBatchSize = 1000)
        {
            Options.InternUris = false;

            _maxBatchSize = maxBatchSize;

            _actionBlock = new ActionBlock<TripleStore>(async (tripleStore) =>
            {
                await Process(tripleStore);
            },
            new ExecutionDataflowBlockOptions
            { 
                MaxDegreeOfParallelism = 4,
                BoundedCapacity = 4
            });
        }

        protected override async Task EmitPackage(JObject package)
        {
            await base.EmitPackage(package);

            lock (this)
            {
                _packageIds.Add(package["id"].ToString().ToLowerInvariant());
            }

            IGraph graph = Utils.CreateGraph(package);

            lock (this)
            {
                if (_currentStore == null)
                {
                    _currentStore = new TripleStore();
                }

                _currentStore.Add(graph, true);

                int currentBatchSize = Interlocked.Increment(ref _currentBatchSize);

                if (currentBatchSize == _maxBatchSize)
                {
                    _actionBlock.Post(_currentStore);
                    _currentBatchSize = 0;
                    _currentStore = null;
                }
            }
        }

        public override async Task Close()
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

            await base.Close();

            Console.WriteLine("{0} distinct package ids", _packageIds.Count);
        }

        async Task Process(TripleStore store)
        {
            try
            {
                Console.Write("loaded {0:N0} triples (memory: {1:N0} bytes)", store.Triples.Count(), GC.GetTotalMemory(true));

                await Task.Factory.StartNew(() =>
                {
                    SparqlResultSet distinctIds = SparqlHelpers.Select(store, Utils.GetResource("sparql.SelectDistinctPackage.rq"));

                    foreach (SparqlResult row in distinctIds)
                    {
                        string id = row["id"].ToString();

                        lock (this)
                        {
                            _packageIds.Add(id);
                        }
                    }
                });
            }
            finally
            {
                store.Dispose();
            }
        }
    }
}
