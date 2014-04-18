using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.Common.Collections;
using VDS.RDF;
using VDS.RDF.Query.Inference;

namespace GatherMergeRewrite
{
    class StaticRdfsPlusReasoner : IInferenceEngine
    {
        StaticRdfsReasoner _rdfs;
        StaticOwlReasoner _owl;

        public StaticRdfsPlusReasoner()
        {
            _rdfs = new StaticRdfsReasoner();
            _owl = new StaticOwlReasoner();
        }

        public void Apply(IGraph input, IGraph output)
        {
            while (true)
            {
                int before = output.Triples.Count;

                _rdfs.Apply(input, output);
                _owl.Apply(input, output);

                int after = output.Triples.Count;

                if (after == before)
                {
                    break;
                }
            }
        }

        public void Apply(IGraph g)
        {
            Apply(g, g);
        }

        public void Initialise(IGraph g)
        {
            _rdfs.Initialise(g);
            _owl.Initialise(g);
        }
    }
}
