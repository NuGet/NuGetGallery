using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net.Http;

namespace NuGetGallery
{
    public class SearchServiceClient
    {
        public static HashSet<int> GetRangeFromIndex(int minPackageKey, int maxPackageKey, string host)
        {
            string url = string.Format("http://{0}/range?min={1}&max={2}", host, minPackageKey, maxPackageKey);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);

            HttpClient client = new HttpClient();
            HttpResponseMessage response = client.SendAsync(request).Result;

            string content = response.Content.ReadAsStringAsync().Result;

            HashSet<int> result = new HashSet<int>();

            JArray array = JArray.Parse(content);
            foreach (int key in array)
            {
                result.Add(key);
            }

            return result;
        }
    }
}
