using JsonLD.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGet3.Client.Core
{
    public class JsonLdPageCache
    {
        ConcurrentDictionary<Uri, Page> _pages = new ConcurrentDictionary<Uri, Page>();

        public Task<JToken> Fetch(Uri address)
        {
            return Task<JToken>.Run(() =>
            {
                Page page = _pages.GetOrAdd(address, (newPageAddress) => 
                {
                    HttpClient client = new HttpClient();
                    string json = client.GetStringAsync(newPageAddress).Result;
                    JToken compacted = JToken.Parse(json);
                    Page newPage = Page.Create(newPageAddress, compacted);
                    return newPage;
                });

                JToken result;
                page.TryFetch(address.Fragment, out result);

                return result;
            });
        }

        public Task<JToken> FetchArrayItem(JToken obj)
        {
            return Fetch(new Uri(obj["@id"].ToString()));
        }

        public Task<JToken> FetchProperty(JToken obj, string property)
        {
            return Fetch(GetUri(obj, property));
        }
        
        static Uri GetUri(JToken obj, string property)
        {
            return new Uri(obj[property]["@id"].ToString());
        }

        class Page
        {
            Uri _address;
            IDictionary<string, JToken> _fragments = new Dictionary<string, JToken>();

            Page(Uri address)
            {
                _address = address;
            }

            public bool TryFetch(string fragment, out JToken result)
            {
                return _fragments.TryGetValue(fragment, out result);
            }

            public static Page Create(Uri address, JToken compacted)
            {
                Page newPage = new Page(address);

                List<JToken> nodes = new List<JToken>();
                int marker = 0;
                Mark(compacted, ref marker, nodes);
                JToken expanded = JsonLdProcessor.Expand(compacted, new JsonLdOptions());
                Load(expanded, newPage._fragments, address, nodes);
                return newPage;
            }

            static void Load(JToken expanded, IDictionary<string, JToken> fragments, Uri address, List<JToken> nodes)
            {
                foreach (JObject obj in expanded)
                {
                    foreach (JProperty prop in obj.Properties())
                    {
                        if (prop.Name == "@id")
                        {
                            Uri id = new Uri(prop.Value.ToString());

                            // Using the marker, find the corresponding compacted node in the nodes list
                            JToken node;
                            if (((JObject)prop.Parent).TryGetValue("http://nuget.org/cache/node", out node))
                            {
                                int marker = node[0]["@value"].ToObject<int>();
                                JToken compactedChild = nodes[marker];

                                // Add or idempotently overwrite the @id because we use that on a later Fetch (see the function GetUri)
                                compactedChild["@id"] = id.AbsoluteUri;

                                // If this the same address add to the cache (note Uri comparison intentionally ignores fragments) 
                                if (id == address)
                                {
                                    fragments.Add(id.Fragment, compactedChild);
                                }
                            }
                        }
                        else if (prop.Name == "@type")
                        {
                        }
                        else
                        {
                            Load(prop.Value, fragments, address, nodes);
                        }
                    }
                }
            }

            static void Mark(JToken node, ref int marker, List<JToken> nodes)
            {
                if (node is JObject)
                {
                    node["http://nuget.org/cache/node"] = marker++;
                    nodes.Add(node);
                }

                foreach (JToken item in node)
                {
                    if (item is JProperty)
                    {
                        if (((JProperty)item).Name == "@context")
                        {
                            continue;
                        }

                        JToken value = ((JProperty)item).Value;

                        if (value is JObject || value is JArray)
                        {
                            Mark(value, ref marker, nodes);
                        }
                    }
                    else if (item is JObject || item is JArray)
                    {
                        Mark(item, ref marker, nodes);
                    }
                }
            }
        }
    }
}
