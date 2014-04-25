using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;
using VDS.RDF.Query.Inference;

namespace GatherMergeRewrite
{
    class Reasoner
    {
        IList<SparqlQuery> _rules;

        public Reasoner()
        {
            _rules = new List<SparqlQuery>();

            SparqlQueryParser sparqlparser = new SparqlQueryParser();

            _rules.Add(sparqlparser.ParseFromString(Load("rules\\rdfs\\domain.rq")));
            _rules.Add(sparqlparser.ParseFromString(Load("rules\\rdfs\\subClassOf.rq")));
            _rules.Add(sparqlparser.ParseFromString(Load("rules\\owl\\owl.rq")));
            _rules.Add(sparqlparser.ParseFromString(Load("rules\\owl\\SymmetricProperty.rq")));
            _rules.Add(sparqlparser.ParseFromString(Load("rules\\owl\\inverseOf.rq")));
        }

        public void Apply(TripleStore store)
        {
            InMemoryDataset ds = new InMemoryDataset(store);
            ISparqlQueryProcessor processor = new LeviathanQueryProcessor(ds);

            while (true)
            {
                int before = store.Triples.Count();

                foreach (SparqlQuery rule in _rules)
                {
                    IGraph inferred = (IGraph)processor.ProcessQuery(rule);
                    store.Add(inferred, true);
                }

                int after = store.Triples.Count();

                if (after == before)
                {
                    break;
                }
            }
        }

        static string Load(string name)
        {
            return (new StreamReader(Utils.GetResourceStream(name))).ReadToEnd();
        }
    }
}
