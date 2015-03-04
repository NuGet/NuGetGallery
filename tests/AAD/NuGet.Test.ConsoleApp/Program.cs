using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Net.Http.Headers;

namespace NuGet.Test.ConsoleApp
{
    class Program
    {
        public const string ServiceAddress = "https://nugettestaad.azurewebsites.net/claims";

        const string TenantName = "nuget20150223.onmicrosoft.com";
        const string ServiceResourceId = "http://nuget20150223.onmicrosoft.com/nugettestsecureservice";
        const string ClientId = "1b0ad851-7ae2-407e-b23b-6ee41b5262b9";
        static Uri RedirectUri = new Uri("https://lala");
        static string Authority = string.Format("https://login.windows.net/{0}", TenantName);

        static void Main(string[] args)
        {
            AuthenticationContext authenticationContext = new AuthenticationContext(Authority);
            AuthenticationResult authenticationResult = authenticationContext.AcquireToken(ServiceResourceId, ClientId, RedirectUri, PromptBehavior.Always);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, ServiceAddress);
            
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authenticationResult.AccessToken);

            HttpClient client = new HttpClient();
            HttpResponseMessage response = client.SendAsync(request).Result;

            if (response.IsSuccessStatusCode)
            {
                string content = response.Content.ReadAsStringAsync().Result;

                JArray result = JArray.Parse(content);
                foreach (JObject obj in result)
                {
                    string type = obj["type"].ToString();
                    Console.Write(type);
                    int padding = 72 - type.Length;
                    for (int i = 0; i < padding; i++)
                    {
                        Console.Write(" ");
                    }
                    Console.WriteLine(obj["value"]);
                }
            }
            else
            {
                Console.WriteLine(response.StatusCode);
            }
        }
    }
}
