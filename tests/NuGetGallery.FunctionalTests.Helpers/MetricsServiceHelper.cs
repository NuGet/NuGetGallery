using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NuGetGallery.FunctionTests.Helpers
{
    public class MetricsServiceHelper
    {
        public static async Task<bool> TryHitMetricsEndPoint(string id, string version, string ipAddress, string userAgent, string operation, string dependentPackage, string projectGuids)
        {
            var jObject = GetJObject(id, version, ipAddress, userAgent, operation, dependentPackage, projectGuids);
            var result = await TryHitMetricsEndPoint(jObject);
            return result;
        }

        public static async Task<bool> TryHitMetricsEndPoint(JObject jObject)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var requestUri = new Uri(MetricsServiceUri + MetricsDownloadEventMethod);
                    var content = new StringContent(jObject.ToString(), Encoding.UTF8, ContentTypeJson);
                    var response = await httpClient.PostAsync(requestUri, content);

                    //print the header
                    Console.WriteLine("HTTP status code : {0}", response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.Accepted)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            catch (HttpRequestException hre)
            {
                Console.WriteLine("Exception : {0}", hre.Message);
                return false;
            }
        }

        public static JObject GetJObject(string id, string version, string ipAddress, string userAgent, string operation, string dependentPackage, string projectGuids)
        {
            var jObject = new JObject();
            jObject.Add(IdKey, id);
            jObject.Add(VersionKey, version);
            if (!String.IsNullOrEmpty(ipAddress)) jObject.Add(IPAddressKey, ipAddress);
            if (!String.IsNullOrEmpty(userAgent)) jObject.Add(UserAgentKey, userAgent);
            if (!String.IsNullOrEmpty(operation)) jObject.Add(OperationKey, operation);
            if (!String.IsNullOrEmpty(dependentPackage)) jObject.Add(DependentPackageKey, dependentPackage);
            if (!String.IsNullOrEmpty(projectGuids)) jObject.Add(ProjectGuidsKey, projectGuids);

            return jObject;
        }

        public const string IdKey = "id";
        public const string VersionKey = "version";
        public const string IPAddressKey = "ipAddress";
        public const string UserAgentKey = "userAgent";
        public const string OperationKey = "operation";
        public const string DependentPackageKey = "dependentPackage";
        public const string ProjectGuidsKey = "projectGuids";
        public const string HTTPPost = "POST";
        public const string MetricsDownloadEventMethod = "/DownloadEvent";
        public const string ContentTypeJson = "application/json";
        public const string MetricsServiceUri = "http://api-metrics.int.nugettest.org";
    }
}
