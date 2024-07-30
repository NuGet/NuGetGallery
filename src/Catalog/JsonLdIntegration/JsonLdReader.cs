// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using VDS.RDF;
using VDS.RDF.Parsing.Handlers;

namespace NuGet.Services.Metadata.Catalog.JsonLDIntegration
{
    public class JsonLdReader : IRdfReader
    {
        public JsonLdReader()
        {
            if (Warning == null)
            {
                //  this event looks a little brain damaged
            }
        }

        public void Load(IRdfHandler handler, string filename)
        {
            Load(handler, new StreamReader(filename));
        }

        public void Load(IRdfHandler handler, TextReader input)
        {
            bool finished = false;
            try
            {
                // Tell handler we starting parsing
                handler.StartRdf();

                // Perform actual parsing
                using (JsonReader jsonReader = new JsonTextReader(input))
                {
                    jsonReader.DateParseHandling = DateParseHandling.None;

                    JToken json = JToken.Load(jsonReader);

                    foreach (JObject subjectJObject in json)
                    {
                        string subject = subjectJObject["@id"].ToString();

                        JToken type;
                        if (subjectJObject.TryGetValue("@type", out type))
                        {
                            if (type is JArray)
                            {
                                foreach (JToken t in (JArray) type)
                                {
                                    if (!HandleTriple(handler, subject, "http://www.w3.org/1999/02/22-rdf-syntax-ns#type", t.ToString(), null, false)) return;
                                }
                            }
                            else
                            {
                                if (!HandleTriple(handler, subject, "http://www.w3.org/1999/02/22-rdf-syntax-ns#type", type.ToString(), null, false)) return;
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
                                    if (!HandleTriple(handler, subject, property.Name, id.ToString(), null, false)) return;
                                }
                                else if (objectJObject.TryGetValue("@value", out value))
                                {
                                    string datatype = null;
                                    JToken datatypeJToken;
                                    if (objectJObject.TryGetValue("@type", out datatypeJToken))
                                    {
                                        datatype = datatypeJToken.ToString();
                                    }
                                    else
                                    {
                                        switch (value.Type)
                                        {
                                            case JTokenType.Boolean:
                                                datatype = "http://www.w3.org/2001/XMLSchema#boolean";
                                                break;
                                            case JTokenType.Float:
                                                datatype = "http://www.w3.org/2001/XMLSchema#double";
                                                break;
                                            case JTokenType.Integer:
                                                datatype = "http://www.w3.org/2001/XMLSchema#integer";
                                                break;
                                        }
                                    }

                                    if (!HandleTriple(handler, subject, property.Name, value.ToString(), datatype, true)) return;
                                }
                            }
                        }
                    }
                }

                // Tell handler we've finished parsing
                finished = true;
                handler.EndRdf(true);
            }
            catch
            {
                // Catch all block to fulfill the IRdfHandler contract of informing the handler when the parsing has ended with failure
                finished = true;
                handler.EndRdf(false);
                throw;
            }
            finally
            {
                // Finally block handles the case where we exit the parsing loop early because the handler indicated it did not want
                // to receive further triples.  In this case finished will be set to false and we need to inform the handler we're are done
                if (!finished)
                {
                    handler.EndRdf(true);
                }
            }
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
            Load(new GraphHandler(g), input);
        }

        public void Load(IGraph g, StreamReader input)
        {
            Load(g, (TextReader)input);
        }

        public event RdfReaderWarning Warning;

        /// <summary>
        /// Creates and handles a triple
        /// </summary>
        /// <param name="handler">Handler</param>
        /// <param name="subject">Subject</param>
        /// <param name="predicate">Predicate</param>
        /// <param name="obj">Object</param>
        /// <param name="datatype">Object Datatype</param>
        /// <param name="isLiteral">isLiteral Object</param>
        /// <returns>True if parsing should continue, false otherwise</returns>
        bool HandleTriple(IRdfHandler handler, string subject, string predicate, string obj, string datatype, bool isLiteral)
        {
            INode subjectNode;
            if (subject.StartsWith("_"))
            {
                string nodeId = subject.Substring(subject.IndexOf(":") + 1);
                subjectNode = handler.CreateBlankNode(nodeId);
            }
            else
            {
                subjectNode = handler.CreateUriNode(new Uri(subject));
            }

            INode predicateNode = handler.CreateUriNode(new Uri(predicate));
            
            INode objNode;
            if (isLiteral)
            {
                if (datatype == "http://www.w3.org/2001/XMLSchema#boolean")
                {
                    //  sometimes newtonsoft.json appears to return boolean as string True and dotNetRdf doesn't appear to recognize that
                    obj = ((string)obj).ToLowerInvariant();
                }

                objNode = (datatype == null) ? handler.CreateLiteralNode((string)obj) : handler.CreateLiteralNode((string)obj, new Uri(datatype));
            }
            else
            {
                if (obj.StartsWith("_"))
                {
                    string nodeId = obj.Substring(obj.IndexOf(":") + 1);
                    objNode = handler.CreateBlankNode(nodeId);
                }
                else
                {
                    objNode = handler.CreateUriNode(new Uri(obj));
                }
            }

            return handler.HandleTriple(new Triple(subjectNode, predicateNode, objNode));
        }
    }
}
