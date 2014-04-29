using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VDS.RDF;

namespace Catalog
{
    public class TripleStorePackageEmitter : CountingPackageEmitter
    {
        TripleStore _store;

        public TripleStorePackageEmitter(TripleStore store)
        {
            _store = store;
        }

        protected override void EmitPackage(JObject package)
        {
            base.EmitPackage(package);

            IGraph graph = Utils.CreateGraph(package);

            lock (this)
            {
                _store.Add(graph, true);
            }
        }

        public override void Close()
        {
            base.Close();
        }
    }
}
