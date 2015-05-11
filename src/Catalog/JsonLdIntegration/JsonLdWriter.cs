// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VDS.RDF;
using Newtonsoft.Json;

namespace NuGet.Services.Metadata.Catalog.JsonLDIntegration
{
    public class JsonLdWriter : IRdfWriter
    {
        public JsonLdWriter()
        {
            if (Warning == null)
            {
                //  this event looks a little brain damaged
            }
        }

        public void Save(IGraph g, TextWriter output)
        {
            JToken flattened = MakeExpandedForm(g);

            output.Write(flattened.ToString());
            output.Flush();
        }

        public void Save(IGraph g, string filename)
        {
            Save(g, new StreamWriter(filename));
        }

        public event RdfWriterWarning Warning;

        const string First = "http://www.w3.org/1999/02/22-rdf-syntax-ns#first";
        const string Rest = "http://www.w3.org/1999/02/22-rdf-syntax-ns#rest";
        const string Nil = "http://www.w3.org/1999/02/22-rdf-syntax-ns#nil";

        static IDictionary<INode, List<INode>> GetLists(IGraph graph)
        {
            INode first = graph.CreateUriNode(new Uri(First));
            INode rest = graph.CreateUriNode(new Uri(Rest));
            INode nil = graph.CreateUriNode(new Uri(Nil));

            IEnumerable<Triple> ends = graph.GetTriplesWithPredicateObject(rest, nil);

            IDictionary<INode, List<INode>> lists = new Dictionary<INode, List<INode>>();

            foreach (Triple end in ends)
            {
                List<INode> list = new List<INode>();

                Triple iterator = graph.GetTriplesWithSubjectPredicate(end.Subject, first).First();

                INode head = iterator.Subject;

                while (true)
                {
                    list.Add(iterator.Object);

                    IEnumerable<Triple> restTriples = graph.GetTriplesWithPredicateObject(rest, iterator.Subject);

                    if (restTriples.Count() == 0)
                    {
                        break;
                    }

                    iterator = graph.GetTriplesWithSubjectPredicate(restTriples.First().Subject, first).First();

                    head = iterator.Subject;
                }

                list.Reverse();

                lists.Add(head, list);
            }

            return lists;
        }

        static bool IsListNode(INode subject, IGraph graph)
        {
            INode rest = graph.CreateUriNode(new Uri(Rest));
            return (graph.GetTriplesWithSubjectPredicate(subject, rest).Count() > 0);
        }

        JToken MakeExpandedForm(IGraph graph)
        {
            IDictionary<INode, List<INode>> lists = GetLists(graph);

            IDictionary<string, JObject> subjects = new Dictionary<string, JObject>();

            foreach (Triple triple in graph.Triples.Where((t) => !IsListNode(t.Subject, graph)))
            {
                string subject = triple.Subject.ToString();
                string predicate = triple.Predicate.ToString();

                if (predicate == "http://www.w3.org/1999/02/22-rdf-syntax-ns#type")
                {
                    predicate = "@type";
                }

                JObject properties;
                if (!subjects.TryGetValue(subject, out properties))
                {
                    properties = new JObject();
                    properties.Add("@id", subject);
                    subjects.Add(subject, properties);
                }

                JArray objects;
                JToken o;
                if (!properties.TryGetValue(predicate, out o))
                {
                    objects = new JArray();
                    properties.Add(predicate, objects);
                }
                else
                {
                    objects = (JArray)o;
                }

                if (predicate == "@type")
                {
                    objects.Add(triple.Object.ToString());
                }
                else
                {
                    if (lists.ContainsKey(triple.Object))
                    {
                        objects.Add(MakeList(lists[triple.Object]));
                    }
                    else
                    {
                        objects.Add(MakeObject(triple.Object));
                    }
                }
            }

            JArray result = new JArray();
            foreach (JObject subject in subjects.Values)
            {
                result.Add(subject);
            }

            return result;
        }

        static JToken MakeList(List<INode> nodes)
        {
            JArray list = new JArray();

            foreach (INode node in nodes)
            {
                list.Add(MakeObject(node));
            }

            return new JObject { { "@list", list } };
        }

        static JToken MakeObject(INode node)
        {
            if (node is IUriNode)
            {
                return new JObject { { "@id", node.ToString() } };
            }
            else if (node is IBlankNode)
            {
                return new JObject { { "@id", node.ToString() } };
            }
            else
            {
                return MakeLiteralObject((ILiteralNode)node);
            }
        }

        static JObject MakeLiteralObject(ILiteralNode node)
        {
            if (node.DataType == null)
            {
                return new JObject { { "@value", node.Value } };
            }
            else
            {
                string dataType = node.DataType.ToString();

                switch (dataType)
                {
                    case "http://www.w3.org/2001/XMLSchema#integer":
                        return new JObject { { "@value", int.Parse(node.Value) } };
                    
                    case "http://www.w3.org/2001/XMLSchema#boolean":
                        return new JObject { { "@value", bool.Parse(node.Value) } };
                    
                    case "http://www.w3.org/2001/XMLSchema#decimal":
                        return new JObject { { "@value", decimal.Parse(node.Value) } };

                    case "http://www.w3.org/2001/XMLSchema#long":
                        return new JObject { { "@value", long.Parse(node.Value) } };

                    case "http://www.w3.org/2001/XMLSchema#short":
                        return new JObject { { "@value", short.Parse(node.Value) } };

                    case "http://www.w3.org/2001/XMLSchema#float":
                        return new JObject { { "@value", float.Parse(node.Value) } };

                    case "http://www.w3.org/2001/XMLSchema#double":
                        return new JObject { { "@value", double.Parse(node.Value) } };

                    default:
                        return new JObject 
                        {
                            { "@value", node.Value },
                            { "@type", dataType }
                        };
                }
            }
        }
    }
}
