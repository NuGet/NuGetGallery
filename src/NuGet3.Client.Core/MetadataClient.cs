using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NuGet3.Client
{
    public class MetadataClient
    {
        private ServiceClient serviceClient;

        public MetadataClient(ServiceClient serviceClient)
        {
            // TODO: Complete member initialization
            this.serviceClient = serviceClient;
        }

        public async Task<string> FindPackage(string packageId, string packageVersion)
        {
            HttpClient hc = new HttpClient();
            string index = await hc.GetStringAsync(serviceClient.AllPackages);
            JObject indexJson = JObject.Parse(index);

            var segments = ((JArray)indexJson["entry"]).OrderBy(page => (string)page["lowest"], StringComparer.InvariantCultureIgnoreCase).Select(j => (JObject)j).ToArray();

            int i;
            for (i = 0; i < segments.Count(); ++i)
            {
                if (string.Compare((string)segments[i]["lowest"], packageId, true, CultureInfo.InvariantCulture) > 0)
                {
                    break;
                }
            }

            if (i == 0)
            {
                return null;
            }

            string segment = await hc.GetStringAsync((string)segments[i - 1]["url"]);
            JObject segmentJson = JObject.Parse(segment);
            foreach (var entry in segmentJson["entry"])
            {
                if (string.Compare((string)entry["id"],packageId,true,CultureInfo.InvariantCulture) == 0)
                {
                    return (string)entry["url"];
                }
            }

            return null;
        }

        public PackageMetadata GetPackageMetadata(string packageUri)
        {
            return new PackageMetadata();
        }
    }
}
