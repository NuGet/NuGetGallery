using JsonLD.Core;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using VDS.RDF;

namespace JsonLDIntegration
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

        public JToken Frame
        {
            get;
            set;
        }

        public void Save(IGraph g, TextWriter output)
        {
            JToken flattened = MakeExpandedForm(g);

            System.Console.WriteLine(flattened);

            if (Frame == null)
            {
                output.Write(flattened);
            }
            else
            {
                JObject framed = JsonLdProcessor.Frame(flattened, Frame, new JsonLdOptions());
                JObject compacted = JsonLdProcessor.Compact(framed, framed["@context"], new JsonLdOptions());
                output.Write(compacted);
            }
        }

        public void Save(IGraph g, string filename)
        {
            Save(g, new StreamWriter(filename));
        }

        public event RdfWriterWarning Warning;

        JToken MakeExpandedForm(IGraph graph)
        {
            IDictionary<string, JObject> subjects = new Dictionary<string, JObject>();

            foreach (Triple triple in graph.Triples)
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
                    objects.Add(MakeObject(triple.Object));
                }
            }

            JArray result = new JArray();
            foreach (JObject subject in subjects.Values)
            {
                result.Add(subject);
            }

            return result;
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
