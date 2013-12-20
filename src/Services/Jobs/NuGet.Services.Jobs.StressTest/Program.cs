using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Jobs.StressTest
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Spin up multiple invocations of various kinds
            string[] jobs = new[] {
                "Ping",
                "Async"
            };
            var r = new Random();
            var client = new HttpClient(new WebRequestHandler());
            Parallel.For(0, 100, new ParallelOptions() { MaxDegreeOfParallelism = 10 }, i =>
            {
                // Pick a random test
                string job = jobs[r.Next(0, jobs.Length)];
                var resp = client.PutAsync("http://localhost:8080/invocations", new StringContent(
                    "{job:'" + job + "', source:'stresstest'}",
                    Encoding.UTF8,
                    "application/json")).Result;
                if (resp.IsSuccessStatusCode)
                {
                    dynamic obj = JObject.Parse(resp.Content.ReadAsStringAsync().Result);
                    Console.WriteLine("200 OK: {0} - {1}", job, obj.Id);
                }
                else
                {
                    Console.WriteLine("{0} {1}: {2}!", (int)resp.StatusCode, resp.StatusCode, job);
                }
            });
        }
    }
}
