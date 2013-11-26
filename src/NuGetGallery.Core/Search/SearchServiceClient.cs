using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace NuGetGallery
{
    public class SearchServiceClient
    {
        public static IDictionary<int, int> GetRangeFromIndex(int minPackageKey, int maxPackageKey, string host)
        {
            string url = string.Format("http://{0}/range?min={1}&max={2}", host, minPackageKey, maxPackageKey);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);

            HttpClient client = new HttpClient();
            HttpResponseMessage response = client.SendAsync(request).Result;

            if (response.IsSuccessStatusCode)
            {
                string content = response.Content.ReadAsStringAsync().Result;
                IDictionary<int, int> result = new Dictionary<int, int>();
                JObject obj = JObject.Parse(content);
                foreach (KeyValuePair<string, JToken> property in obj)
                {
                    result.Add(int.Parse(property.Key), property.Value.Value<int>());
                }
                return result;
            }
            else
            {
                string content = string.Empty;
                try
                {
                    content = response.Content.ReadAsStringAsync().Result;
                }
                catch (Exception) { }
                throw new Exception(string.Format("HTTP status = {0} content = {1}", response.StatusCode, content));
            }
        }

        public static JObject Search(
            string q, 
            string projectType,
            bool prerelease,
            bool countOnly,
            string feed,
            string sortBy,
            int page,
            string host)
        {
            IDictionary<string, string> nameValue = new Dictionary<string, string>();
            nameValue.Add("q", q);
            nameValue.Add("projectType", projectType);
            nameValue.Add("prerelease", prerelease.ToString());
            nameValue.Add("countOnly", countOnly.ToString());
            nameValue.Add("feed", feed);
            nameValue.Add("sortBy", sortBy);
            nameValue.Add("page", page.ToString());

            FormUrlEncodedContent query = new FormUrlEncodedContent(nameValue);
            string url = string.Format("http://{0}/search?{1}", host, query.ReadAsStringAsync().Result);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);

            HttpClient client = new HttpClient();
            HttpResponseMessage response = client.SendAsync(request).Result;

            if (response.IsSuccessStatusCode)
            {
                string content = response.Content.ReadAsStringAsync().Result;
                JObject obj = JObject.Parse(content);
                return obj;
            }
            else
            {
                string content = string.Empty;
                try
                {
                    content = response.Content.ReadAsStringAsync().Result;
                }
                catch (Exception) { }
                throw new Exception(string.Format("HTTP status = {0} content = {1}", response.StatusCode, content));
            }
        }
    }
}
