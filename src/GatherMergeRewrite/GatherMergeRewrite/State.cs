using System;
using System.Collections.Generic;
using VDS.RDF;

namespace GatherMergeRewrite
{
    class State
    {
        public TripleStore Store { get; private set; }
        public IDictionary<Uri, Tuple<string, string>> Resources { get; private set; }

        public State()
        {
            Store = new TripleStore();
            Resources = new Dictionary<Uri, Tuple<string, string>>();
        }
    }
}
