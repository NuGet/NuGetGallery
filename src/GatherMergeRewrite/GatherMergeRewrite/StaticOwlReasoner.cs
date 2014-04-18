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
    class StaticOwlReasoner : IInferenceEngine
    {
        IDictionary<INode, HashSet<INode>> _inverseOfMappings = new Dictionary<INode, HashSet<INode>>();

        IUriNode _rdfType;
        IUriNode _owlInverseOf;
        IUriNode _owlSymmetricProperty;

        public StaticOwlReasoner()
        {
            Graph g = new Graph();
            g.NamespaceMap.AddNamespace("rdf", new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#"));
            g.NamespaceMap.AddNamespace("owl", new Uri("http://www.w3.org/2002/07/owl#"));
            _rdfType = g.CreateUriNode("rdf:type");
            _owlInverseOf = g.CreateUriNode("owl:inverseOf");
            _owlSymmetricProperty = g.CreateUriNode("owl:SymmetricProperty");
        }

        public void Apply(IGraph input, IGraph output)
        {
            List<Triple> inferences = new List<Triple>();
            foreach (Triple t in input.Triples)
            {
                if (_inverseOfMappings.ContainsKey(t.Predicate))
                {
                    foreach (INode node in _inverseOfMappings[t.Predicate])
                    {
                        inferences.Add(new Triple(t.Object.CopyNode(output), node.CopyNode(output), t.Subject.CopyNode(output)));
                    }
                }
            }

            inferences.RemoveAll(t => t.Subject.NodeType == NodeType.Literal);
            if (inferences.Count > 0)
            {
                output.Assert(inferences);
            }
        }

        public void Apply(IGraph g)
        {
            Apply(g, g);
        }

        public void Initialise(IGraph g)
        {
            foreach (Triple t in g.Triples)
            {
                if (t.Predicate.Equals(_owlInverseOf))
                {
                    AddInverseMapping(t.Subject, t.Object);
                    AddInverseMapping(t.Object, t.Subject);
                }

                if (t.Predicate.Equals(_rdfType) && t.Object.Equals(_owlSymmetricProperty))
                {
                    AddInverseMapping(t.Subject, t.Subject);
                }
            }
        }

        private void AddInverseMapping(INode x, INode y)
        {
            HashSet<INode> values;
            if (!_inverseOfMappings.TryGetValue(x, out values))
            {
                values = new HashSet<INode>();
                _inverseOfMappings.Add(x, values);
            }
            values.Add(y);
        }
    }
}
