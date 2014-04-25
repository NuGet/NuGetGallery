using System;
using System.Collections.Generic;
using VDS.RDF;
using VDS.RDF.Query.Inference;

namespace GatherMergeRewrite
{
    class State
    {
        public State(string container, string baseAddress)
        {
            Store = new TripleStore();

            IGraph schema = Utils.Load("schema\\schema.ttl");

            //IInferenceEngine rdfsPlus = new StaticRdfsPlusReasoner();
            //rdfsPlus.Initialise(schema);
            //Store.AddInferenceEngine(rdfsPlus);

            Store.Add(schema, true);

            Resources = new Dictionary<Uri, Tuple<string, string, string>>();
            Container = container;
            BaseAddress = baseAddress;

            Reasoner = new Reasoner();
        }

        public Reasoner Reasoner
        {
            get;
            private set;
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
