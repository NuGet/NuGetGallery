using System;
using System.Collections.Generic;
using VDS.RDF;

namespace GatherMergeRewrite
{
    class State
    {
        public State(string container, string baseAddress)
        {
            Store = new TripleStore();
            Resources = new Dictionary<Uri, Tuple<string, string>>();
            Container = container;
            BaseAddress = baseAddress;
        }

        public TripleStore Store 
        { 
            get; 
            private set; 
        }

        public IDictionary<Uri, Tuple<string, string>> Resources 
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
