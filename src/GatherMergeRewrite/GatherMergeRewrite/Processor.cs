using JsonLDIntegration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;
using VDS.RDF.Writing;

namespace GatherMergeRewrite
{
    public class Processor
    {
        public static string Container = "";
        public static string BaseAddress = "";
        public static string ConnectionString = "";

        public static void Upload(string ownerId, string registrationId, string nupkg, DateTime published)
        {
            string address = string.Format("{0}/{1}/", BaseAddress, Container);

            Uri ownerUri = new Uri(address + ownerId);
            Uri registrationUri = new Uri(address + registrationId);

            //  (0) create store (in memory)

            TripleStore store = new TripleStore();

            // Phase #1 - new data into a graphs

            IGraph graph = new Graph();

            graph.NamespaceMap.AddNamespace("rdf", new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#"));
            graph.NamespaceMap.AddNamespace("nuget", new Uri("http://nuget.org/schema#"));

            graph.Assert(
                graph.CreateUriNode(ownerUri),
                graph.CreateUriNode("rdf:type"),
                graph.CreateUriNode("nuget:Owner"));

            graph.Assert(
                graph.CreateUriNode(registrationUri),
                graph.CreateUriNode("rdf:type"),
                graph.CreateUriNode("nuget:PackageRegistration"));

            graph.Assert(
                graph.CreateUriNode(registrationUri),
                graph.CreateUriNode("nuget:owner"),
                graph.CreateUriNode(ownerUri));

            graph.Assert(
                graph.CreateUriNode(ownerUri),
                graph.CreateUriNode("nuget:registration"),
                graph.CreateUriNode(registrationUri));

            store.Add(graph, true);

            KeyValuePair<Uri, IGraph> newResource = CreateGraph(Utils.Extract(nupkg), address, ownerUri, published);

            //Debug.DumpTurtle(Utils.GetName(newResource.Key, BaseAddress, Container) + "_pre.ttl", newResource.Value);

            // Phase #2 iterate loading potententially updated resources - stop when we have them all

            IDictionary<Uri, Tuple<string, string>> resources = new Dictionary<Uri, Tuple<string, string>>();

            store.Add(newResource.Value, true);

            //Dump(Construct(store, new StreamReader("all.rq").ReadToEnd()));

            resources.Add(newResource.Key, new Tuple<string, string>("Package.rq", "PackageFrame.json"));

            while (true)
            {
                IDictionary<Uri, Tuple<string, string>> resourceList = DetermineResourceList(store);

                IDictionary<Uri, Tuple<string, string>> missing = new Dictionary<Uri, Tuple<string, string>>();
                foreach (KeyValuePair<Uri, Tuple<string, string>> item in resourceList)
                {
                    if (!resources.ContainsKey(item.Key))
                    {
                        missing.Add(item);
                    }
                }

                if (missing.Count() == 0)
                {
                    break;
                }

                foreach (KeyValuePair<Uri, Tuple<string, string>> item in missing)
                {
                    IGraph resourceGraph = Utils.LoadResourceGraph(ConnectionString, Container, Utils.GetName(item.Key, BaseAddress, Container));

                    if (resourceGraph != null)
                    {
                        store.Add(resourceGraph, true);
                    }

                    resources.Add(item);
                }
            }

            //DEBUG DEBUG DEBUG
            IGraph resourceAllGraph = Utils.Construct(store, new StreamReader("sparql\\All.rq").ReadToEnd());
            Debug.DumpTurtle("all.ttl", resourceAllGraph);

            // Phase #3 save everything - the save is a query and save so recreates each blob with updated content

            foreach (KeyValuePair<Uri, Tuple<string, string>> resource in resources)
            {
                SparqlParameterizedString sparql = new SparqlParameterizedString();
                sparql.CommandText = (new StreamReader("sparql\\" + resource.Value.Item1)).ReadToEnd();
                sparql.SetUri("resource", resource.Key);

                IGraph resourceGraph = Utils.Construct(store, sparql.ToString());

                JToken resourceFrame;
                using (JsonReader jsonReader = new JsonTextReader(new StreamReader("context\\" + resource.Value.Item2)))
                {
                    resourceFrame = JToken.Load(jsonReader);
                }

                Utils.Save(resourceGraph, resourceFrame, ConnectionString, Container, Utils.GetName(resource.Key, BaseAddress, Container));

                //DEBUG DEBUG DEBUG
                Debug.DumpTurtle(Utils.GetName(resource.Key, BaseAddress, Container) + ".ttl", resourceGraph);
            }
        }

        static KeyValuePair<Uri, IGraph> CreateGraph(XDocument nuspec, string baseAddress, Uri ownerUri, DateTime published)
        {
            IGraph graph = Utils.Load(nuspec, baseAddress);

            graph.NamespaceMap.AddNamespace("rdf", new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#"));
            graph.NamespaceMap.AddNamespace("nuget", new Uri("http://nuget.org/schema#"));

            Triple triple = graph.GetTriplesWithPredicateObject(graph.CreateUriNode("rdf:type"), graph.CreateUriNode("nuget:Package")).First();

            graph.Assert(triple.Subject, graph.CreateUriNode("nuget:owner"), graph.CreateUriNode(ownerUri));
            graph.Assert(triple.Subject, graph.CreateUriNode("nuget:published"), graph.CreateLiteralNode(published.ToString()));

            return new KeyValuePair<Uri, IGraph>(((UriNode)triple.Subject).Uri, graph);
        }

        static IDictionary<Uri, Tuple<string, string>> DetermineResourceList(TripleStore store)
        {
            IDictionary<Uri, Tuple<string, string>> resources = new Dictionary<Uri, Tuple<string, string>>();

            SparqlResultSet results = Utils.Select(store, (new StreamReader("sparql\\ListResources.rq")).ReadToEnd());
            foreach (SparqlResult result in results)
            {
                Tuple<string, string> metadata = new Tuple<string, string>(result["transform"].ToString(), result["frame"].ToString());

                resources.Add(new Uri(result["resource"].ToString()), metadata);
            }

            return resources;
        }
    }
}
