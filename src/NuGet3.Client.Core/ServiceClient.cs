using JsonLD.Core;
using Newtonsoft.Json.Linq;
using NuGet3.Client.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NuGet3.Client
{
    public class ServiceClient
    {
        private Uri _serviceBase;
        private string _serviceIndex;
        private TaskCompletionSource<bool> _initialized;

        public string AllPackages { get; private set; }

        // This shouldn't really be here, but need to fix up the resolver to
        // look up universal names from the all packages segment blobs.
        public string ResolverBaseUrl { get; private set; }

        public Task Initialized { get; private set; }

        private Dictionary<string, List<JObject>> _services;

        public ServiceClient(PackageSources sources)
        {
            _initialized = new TaskCompletionSource<bool>();
            Initialized = _initialized.Task;

            _serviceBase = new Uri(sources.Sources().First().Url);
            _services = new Dictionary<string, List<JObject>>();

            WebRequest wr = HttpWebRequest.Create(_serviceBase);
            wr.BeginGetResponse(GetServiceIndex, wr);
        }

        private void GetServiceIndex(IAsyncResult ar)
        {
            WebResponse wr = ((WebRequest)ar.AsyncState).EndGetResponse(ar);
            Stream str = wr.GetResponseStream();
            StreamReader reader = new StreamReader(str);
            _serviceIndex = reader.ReadToEnd();

            JObject serviceIndexJson = JObject.Parse(_serviceIndex);

            AllPackages = (string)serviceIndexJson["allVersions"];
            ResolverBaseUrl = (string)serviceIndexJson["resolverBaseAddress"];

            _initialized.SetResult(true);

            //JToken expanded = JsonLdProcessor.Expand(serviceIndexJson, new JsonLdOptions());

            //foreach (JToken entry in expanded[0]["http://nuget.org/schema#service"])
            //{
            //    foreach (string type in entry["@type"])
            //    {
            //        if (!_services.ContainsKey(type))
            //        {
            //            _services[type] = new List<JObject>();
            //        }
            //        _services[type].Add((JObject)entry);
            //    }
            //}
        }
    }
}
