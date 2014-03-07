using JsonLD.Core;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using VDS.RDF;

namespace MakeMetadata
{
    class JsonLdWriter : IRdfWriter
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

        JToken MakeExpandedForm(IGraph g)
        {
            IDictionary<string, INode> subjects = new Dictionary<string, INode>();

            foreach (Triple triple in g.Triples)
            {
                subjects[triple.Subject.ToString()] = triple.Subject;
            }

            JArray result = new JArray();

            foreach (INode subject in subjects.Values)
            {
                JObject subjectJson = new JObject();
                subjectJson.Add("@id", subject.ToString());
                foreach (Triple withSubject in g.GetTriplesWithSubject(subject))
                {
                    JArray objectArray = new JArray();

                    foreach (Triple withSubjectPredicate in g.GetTriplesWithSubjectPredicate(subject, withSubject.Predicate))
                    {
                        JObject objectObject = new JObject();

                        if (withSubjectPredicate.Object is IUriNode)
                        {
                            objectObject.Add("@id", withSubjectPredicate.Object.ToString());
                        }
                        else if (withSubjectPredicate.Object is IBlankNode)
                        {
                            objectObject.Add("@id", withSubjectPredicate.Object.ToString());
                        }
                        else if (withSubjectPredicate.Object is ILiteralNode)
                        {
                            objectObject.Add("@value", withSubjectPredicate.Object.ToString());
                            if (((ILiteralNode)withSubjectPredicate.Object).DataType != null)
                            {
                                objectObject.Add("@type", ((ILiteralNode)withSubjectPredicate.Object).DataType.ToString());
                            }
                        }

                        objectArray.Add(objectObject);
                    }

                    if (withSubject.Predicate.ToString() == "http://www.w3.org/1999/02/22-rdf-syntax-ns#type")
                    {
                        subjectJson.Add("@type", objectArray[0]["@id"]);
                    }
                    else
                    {
                        string predicate = withSubject.Predicate.ToString();
                        JToken a;
                        if (subjectJson.TryGetValue(predicate, out a))
                        {
                            JArray array = (JArray)a;
                            foreach (JToken t in objectArray)
                            {
                                array.Add(t);
                            }
                        }
                        else
                        {
                            subjectJson.Add(predicate, objectArray);
                        }
                    }
                }
                result.Add(subjectJson);
            }

            return result;
        }
    }
}
