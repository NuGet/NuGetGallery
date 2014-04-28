using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using VDS.RDF;

namespace Catalog.JsonLDIntegration
{
    public class JsonLdReader : IRdfReader
    {
        public JsonLdReader()
        {
            if (Warning == null)
            {
            }
        }

        public void Load(IRdfHandler handler, string filename)
        {
            Load(handler, new StreamReader(filename));
        }

        public void Load(IRdfHandler handler, TextReader input)
        {
            throw new NotImplementedException();
        }

        public void Load(IRdfHandler handler, StreamReader input)
        {
            Load(handler, (TextReader)input);
        }

        public void Load(IGraph g, string filename)
        {
            Load(g, new StreamReader(filename));
        }

        public void Load(IGraph g, TextReader input)
        {
            JToken json;
            using (JsonReader jsonReader = new JsonTextReader(input))
            {
                json = JToken.Load(jsonReader);

                foreach (JObject subjectJObject in json)
                {
                    string subject = subjectJObject["@id"].ToString();

                    JToken type;
                    if (subjectJObject.TryGetValue("@type", out type))
                    {
                        if (type is JArray)
                        {
                            foreach (JToken t in (JArray)type)
                            {
                                Assert(g, subject, "http://www.w3.org/1999/02/22-rdf-syntax-ns#type", new Uri(t.ToString()), null);
                            }
                        }
                        else
                        {
                            Assert(g, subject, "http://www.w3.org/1999/02/22-rdf-syntax-ns#type", new Uri(type.ToString()), null);
                        }
                    }

                    foreach (JProperty property in subjectJObject.Properties())
                    {
                        if (property.Name == "@id" || property.Name == "@type")
                        {
                            continue;
                        }

                        foreach (JObject objectJObject in property.Value)
                        {
                            JToken id;
                            JToken value;
                            if (objectJObject.TryGetValue("@id", out id))
                            {
                                Assert(g, subject, property.Name, new Uri(id.ToString()), null);
                            }
                            else if (objectJObject.TryGetValue("@value", out value))
                            {
                                string datatype = null;
                                JToken datatypeJToken;
                                if (objectJObject.TryGetValue("@type", out datatypeJToken))
                                {
                                    datatype = datatypeJToken.ToString();
                                }
                                Assert(g, subject, property.Name, value.ToString(), datatype);
                            }
                        }
                    }
                }
            }
        }

        public void Load(IGraph g, StreamReader input)
        {
            Load(g, (TextReader)input);
        }

        public event RdfReaderWarning Warning;

        void Assert(IGraph g, string subject, string predicate, object obj, string datatype)
        {
            INode subjectNode;
            if (subject.StartsWith("_"))
            {
                string nodeId = subject.Substring(subject.IndexOf(":") + 1);
                subjectNode = g.CreateBlankNode(nodeId);
            }
            else
            {
                subjectNode = g.CreateUriNode(new Uri(subject));
            }

            INode predicateNode = g.CreateUriNode(new Uri(predicate));
            
            INode objNode;

            if (obj is Uri)
            {
                objNode = g.CreateUriNode((Uri)obj);
            }
            else
            {
                objNode = (datatype == null) ? g.CreateLiteralNode((string)obj) : g.CreateLiteralNode((string)obj, new Uri(datatype));
            }

            g.Assert(subjectNode, predicateNode, objNode);
        }
    }
}
