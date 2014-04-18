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
        public static async Task Upload(IInputDataHandle[] handles, IStorage storage)
        {
            State state = new State(storage.Container, storage.BaseAddress);

            try
            {
                //  (1)

                await CaptureData(state, handles);

                //  (2)

                await LoadResources(state, storage);

                //  (3)

                await SaveResources(state, storage);
            }
            catch (Exception e)
            {
                throw new ProcessorException(state, "Exception in Upload", e);
            }
        }

        static async Task CaptureData(State state, IInputDataHandle[] handles)
        {
            string baseAddress = string.Format("{0}/{1}/", state.BaseAddress, state.Container);

            foreach (IInputDataHandle handle in handles)
            {
                IGraph graph;
                try 
                { 
                    graph = await handle.CreateGraph(baseAddress);
                }
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

        static async Task LoadResources(State state, IStorage storage)
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

                List<Task> tasks = new List<Task>();
                foreach (KeyValuePair<Uri, Tuple<string, string>> item in missing)
                {
                    tasks.Add(storage.Load(Utils.GetName(item.Key, state.BaseAddress, state.Container)));
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

                    foreach (KeyValuePair<Uri, Tuple<string, string>> item in missing)
                    {
                        state.Resources.Add(item);
                    }
                });
            }
        }

        static async Task SaveResources(State state, IStorage storage)
        {
            List<Task> tasks = new List<Task>();

            foreach (KeyValuePair<Uri, Tuple<string, string>> resource in state.Resources)
            {
                SparqlParameterizedString sparql = new SparqlParameterizedString();
                sparql.CommandText = (new StreamReader("sparql\\" + resource.Value.Item1)).ReadToEnd();
                sparql.SetUri("resource", resource.Key);

                IGraph resourceGraph = SparqlHelpers.Construct(state.Store, sparql.ToString());

                JToken resourceFrame;
                using (JsonReader jsonReader = new JsonTextReader(new StreamReader("context\\" + resource.Value.Item2)))
                {
                    resourceFrame = JToken.Load(jsonReader);
                }

                string name = Utils.GetName(resource.Key, state.BaseAddress, state.Container);

                tasks.Add(storage.Save("application/json", name, Utils.CreateJson(resourceGraph, resourceFrame)));

                string htmlName = name;
                if (name.EndsWith(".json"))
                {
                    htmlName = name.Substring(0, name.Length - 5);
                    htmlName = htmlName + ".html";
                }

                tasks.Add(storage.Save("text/html", htmlName, Utils.CreateHtmlView(resource.Key, resourceFrame.ToString(), state.BaseAddress)));
            }

            await Task.Factory.ContinueWhenAll(tasks.ToArray(), (t) => { });
        }

        static IDictionary<Uri, Tuple<string, string>> DetermineResourceList(TripleStore store)
        {
            IDictionary<Uri, Tuple<string, string>> resources = new Dictionary<Uri, Tuple<string, string>>();

            SparqlResultSet results = SparqlHelpers.Select(store, (new StreamReader("sparql\\ListResources.rq")).ReadToEnd());
            foreach (SparqlResult result in results)
            {
                Tuple<string, string> metadata = new Tuple<string, string>(result["transform"].ToString(), result["frame"].ToString());

                resources[new Uri(result["resource"].ToString())] = metadata;
            }

            return resources;
        }
    }
}
