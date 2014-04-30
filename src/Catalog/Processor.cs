using Catalog.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Query;

namespace Catalog
{
    public class Processor
    {
        public static async Task Upload(IInputDataHandle[] handles, Storage storage)
        {
            State state = new State(storage.Container, storage.BaseAddress);

            //  (1)

            await CaptureData(state, handles);

            //  (2)

            await LoadResources(state, storage);

            //  (3)

            await SaveResources(state, storage);
        }

        static async Task CaptureData(State state, IInputDataHandle[] handles)
        {
            string baseAddress = string.Format("{0}{1}/", state.BaseAddress, state.Container);

            foreach (IInputDataHandle handle in handles)
            {
                IGraph graph;
                try
                {
                    graph = await handle.CreateGraph(baseAddress);
                }
                //TODO: this exception handling doesn't look correct
                catch (NuspecMissingException)
                {
                    if (handle is CloudPackageHandle)
                    {
                        using (var log = File.AppendText("log.txt"))
                        {
                            log.WriteLine("Missing nuspec '{0}'.", ((CloudPackageHandle)handle).RegistrationId);
                        }
                    }
                    continue;
                }
                state.Store.Add(graph, true);
            }
        }

        static async Task LoadResources(State state, Storage storage)
        {
            while (true)
            {
                //Utils.Dump(SparqlHelpers.Construct(state.Store, (new StreamReader("sparql\\All.rq")).ReadToEnd()));

                IDictionary<Uri, Tuple<string, string, string>> resourceList = DetermineResourceList(state.Store);

                IDictionary<Uri, Tuple<string, string, string>> missing = new Dictionary<Uri, Tuple<string, string, string>>();
                foreach (KeyValuePair<Uri, Tuple<string, string, string>> item in resourceList)
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

                List<Task> tasks = new List<Task>();
                foreach (KeyValuePair<Uri, Tuple<string, string, string>> item in missing)
                {
                    tasks.Add(storage.Load(item.Key));
                }

                await Task.Factory.ContinueWhenAll(tasks.ToArray(), (tgs) =>
                {
                    foreach (Task<string> tg in tgs)
                    {
                        string json = tg.Result;

                        if (json != null)
                        {
                            IGraph graph = Utils.CreateGraph(json);
                            state.Store.Add(graph, true);
                        }
                    }

                    foreach (KeyValuePair<Uri, Tuple<string, string, string>> item in missing)
                    {
                        state.Resources.Add(item);
                    }
                });
            }
        }

        static async Task SaveResources(State state, Storage storage)
        {
            List<Task> tasks = new List<Task>();

            foreach (KeyValuePair<Uri, Tuple<string, string, string>> resource in state.Resources)
            {
                SparqlParameterizedString sparql = new SparqlParameterizedString();
                sparql.CommandText = Utils.GetResource("sparql." + resource.Value.Item1);
                sparql.SetUri("resource", resource.Key);

                IGraph resourceGraph = SparqlHelpers.Construct(state.Store, sparql.ToString());

                if (resourceGraph.Triples.Count == 0)
                {
                    Utils.Dump(SparqlHelpers.Construct(state.Store, Utils.GetResource("sparql.All.rq")));
                    throw new Exception(string.Format("resource {0} is empty (created by {1})", resource.Key, resource.Value.Item1));
                }

                JToken resourceFrame;
                using (JsonReader jsonReader = new JsonTextReader(new StreamReader(Utils.GetResourceStream("context." + resource.Value.Item2))))
                {
                    resourceFrame = JObject.Load(jsonReader);
                    resourceFrame["@type"] = resource.Value.Item3;
                }

                tasks.Add(storage.Save("application/json", resource.Key, Utils.CreateJson(resourceGraph, resourceFrame)));

                string jsonResourceStr = resource.Key.ToString();
                string htmlResourceStr = jsonResourceStr.Substring(0, jsonResourceStr.Length - 5) + ".html";

                Uri htmlResource = new Uri(htmlResourceStr);

                tasks.Add(storage.Save("text/html", htmlResource, Utils.CreateHtmlView(resource.Key, resourceFrame.ToString(), state.BaseAddress)));
            }

            await Task.Factory.ContinueWhenAll(tasks.ToArray(), (t) => { });
        }

        static IDictionary<Uri, Tuple<string, string, string>> DetermineResourceList(TripleStore store)
        {
            IDictionary<Uri, Tuple<string, string, string>> resources = new Dictionary<Uri, Tuple<string, string, string>>();

            SparqlResultSet results = SparqlHelpers.Select(store, Utils.GetResource("sparql.ListResources.rq"));
            foreach (SparqlResult result in results)
            {
                Tuple<string, string, string> metadata = new Tuple<string, string, string>(
                    result["transform"].ToString(),
                    result["frame"].ToString(),
                    result["type"].ToString());

                resources[new Uri(result["resource"].ToString())] = metadata;
            }

            return resources;
        }
    }
}
