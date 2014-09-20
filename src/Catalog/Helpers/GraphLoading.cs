using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    public static class GraphLoading
    {
        static async Task<IGraph> Load(Uri uri)
        {
            HttpClient client = new HttpClient();
            string json = await client.GetStringAsync(uri);
            return Utils.CreateGraph(json);
        }

        public static async Task<IGraph> Load(Uri root, IDictionary<string, string> rules)
        {
            ISet<Uri> resourceList = new HashSet<Uri>();
            resourceList.Add(root);

            IGraph graph = await Load(root);

            bool dirty = true;

            while (dirty)
            {
                dirty = false;

                foreach (KeyValuePair<string, string> rule in rules)
                {
                    foreach (Triple t1 in graph.GetTriplesWithPredicateObject(
                        graph.CreateUriNode(new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#type")),
                        graph.CreateUriNode(new Uri(rule.Key))))
                    {
                        foreach (Triple t2 in graph.GetTriplesWithSubjectPredicate(
                            t1.Subject,
                            graph.CreateUriNode(new Uri(rule.Value))))
                        {
                            Uri next = ((IUriNode)t2.Object).Uri;

                            if (!resourceList.Contains(next))
                            {
                                IGraph nextGraph = await Load(next);
                                graph.Merge(nextGraph, true);

                                resourceList.Add(next);

                                dirty = true;
                            }
                        }
                    }
                }
            }

            return graph;
        }
    }
}
