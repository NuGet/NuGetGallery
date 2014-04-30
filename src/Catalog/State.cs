using System;
using System.Collections.Generic;
using VDS.RDF;
using VDS.RDF.Query.Inference;

namespace Catalog
{
    class State
    {
        public State(string container, string baseAddress)
        {
            Store = new TripleStore();

            IGraph schema = Utils.Load("schema.schema.ttl");

            //IInferenceEngine rdfs = new StaticRdfsReasoner();
            //rdfs.Initialise(schema);
            //Store.AddInferenceEngine(rdfs);

            Store.Add(schema, true);

            Resources = new Dictionary<Uri, Tuple<string, string, string>>();
            Container = container;
            BaseAddress = baseAddress;
        }

        public TripleStore Store 
        { 
            get; 
            private set; 
        }
        
        public IDictionary<Uri, Tuple<string, string, string>> Resources 
        { 
            get; 
            private set;
        }

        public string Container
        {
            get;
            private set;
        }

        public string BaseAddress
        {
            get;
            private set;
        }
    }
}
