using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using VDS.RDF;
using VDS.RDF.Query;

namespace GatherMergeRewrite
{
    public class Processor
    {
        public static async Task Upload(UploadData data)
        {
            State state = new State();

            //  (1)

            CaptureData(state, data);

            //  (2)

            await LoadResources(state);

            //  (3)

            await SaveResources(state);

            //Debug.Dump(state, data);
        }

        static void CaptureData(State state, UploadData data)
        {
            string address = string.Format("{0}/{1}/", Config.BaseAddress, Config.Container);
            Uri ownerUri = new Uri(address + data.OwnerId);
            Uri registrationUri = new Uri(address + data.RegistrationId);

            IGraph graph = new Graph();

            graph.NamespaceMap.AddNamespace("rdf", new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#"));
            graph.NamespaceMap.AddNamespace("nuget", new Uri("http://nuget.org/schema#"));

            graph.Assert(graph.CreateUriNode(ownerUri), graph.CreateUriNode("rdf:type"), graph.CreateUriNode("nuget:Owner"));
            graph.Assert(graph.CreateUriNode(registrationUri), graph.CreateUriNode("rdf:type"), graph.CreateUriNode("nuget:PackageRegistration"));
            graph.Assert(graph.CreateUriNode(registrationUri), graph.CreateUriNode("nuget:owner"), graph.CreateUriNode(ownerUri));
            graph.Assert(graph.CreateUriNode(ownerUri), graph.CreateUriNode("nuget:registration"), graph.CreateUriNode(registrationUri));

            state.Store.Add(graph, true);

            KeyValuePair<Uri, IGraph> newResource = CreateGraph(Utils.Extract(data.Nupkg), address, ownerUri, data.Published);

            state.Store.Add(newResource.Value, true);

            state.Resources.Add(newResource.Key, new Tuple<string, string>("Package.rq", "PackageFrame.json"));
        }

        static async Task LoadResources(State state)
        {
            while (true)
            {
                IDictionary<Uri, Tuple<string, string>> resourceList = DetermineResourceList(state.Store);

                IDictionary<Uri, Tuple<string, string>> missing = new Dictionary<Uri, Tuple<string, string>>();
                foreach (KeyValuePair<Uri, Tuple<string, string>> item in resourceList)
                {
                    if (!state.Resources.ContainsKey(item.Key))
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
                    IGraph resourceGraph = await Storage.LoadResourceGraph(Utils.GetName(item.Key, Config.BaseAddress, Config.Container));

                    if (resourceGraph != null)
                    {
                        state.Store.Add(resourceGraph, true);
                    }

                    state.Resources.Add(item);
                }
            }
        }

        static async Task SaveResources(State state)
        {
            foreach (KeyValuePair<Uri, Tuple<string, string>> resource in state.Resources)
            {
                SparqlParameterizedString sparql = new SparqlParameterizedString();
                sparql.CommandText = (new StreamReader("sparql\\" + resource.Value.Item1)).ReadToEnd();
                sparql.SetUri("resource", resource.Key);

                IGraph resourceGraph = Utils.Construct(state.Store, sparql.ToString());

                JToken resourceFrame;
                using (JsonReader jsonReader = new JsonTextReader(new StreamReader("context\\" + resource.Value.Item2)))
                {
                    resourceFrame = JToken.Load(jsonReader);
                }

                string name = Utils.GetName(resource.Key, Config.BaseAddress, Config.Container);

                Task t0 = Storage.SaveJson(name, resourceGraph, resourceFrame);
                Task t1 = Storage.SaveHtml(name + ".html", Utils.CreateHtmlView(resource.Key, resourceFrame.ToString()));

                await t0;
                await t1;
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
            try
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
            catch (Exception)
            {
                foreach (Triple triple in store.Triples)
                {
                    Console.WriteLine("{0} {1} {2}", triple.Subject, triple.Predicate, triple.Object);
                }
                throw;
            }
        }
    }
}
