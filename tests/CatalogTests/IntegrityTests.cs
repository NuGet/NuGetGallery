using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Test;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CatalogTests
{
    public class IntegrityTests
    {
        static void ListPackages(JObject range, IDictionary<string, HashSet<string>> results)
        {
            foreach (JObject package in range["items"])
            {
                string id = package["id"].ToString();
                string version = package["version"].ToString();

                HashSet<string> versions;
                if (!results.TryGetValue(id, out versions))
                {
                    versions = new HashSet<string>();
                    results.Add(id, versions);
                }

                versions.Add(version);
            }
        }

        static async Task ListRanges(JObject index, IDictionary<string, HashSet<string>> results)
        {
            foreach (JObject range in index["items"])
            {
                JToken packages;
                if (range.TryGetValue("items", out packages))
                {
                    ListPackages(range, results);
                }
                else
                {
                    HttpClient client = new HttpClient();
                    string json = await client.GetStringAsync(range["url"].ToObject<Uri>());
                    JObject obj = JObject.Parse(json);

                    ListPackages(obj, results);
                }
            }
        }

        public static async Task<IDictionary<string, HashSet<string>>> GetRegistrationPackagesAsync(string path)
        {
            IDictionary<string, HashSet<string>> packages = new Dictionary<string, HashSet<string>>();

            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            foreach (DirectoryInfo registrationDirectoryInfo in directoryInfo.EnumerateDirectories())
            {
                FileInfo[] files = registrationDirectoryInfo.GetFiles("index.json");

                if (files.Length == 0)
                {
                    throw new Exception(string.Format("{0} missing index.json", registrationDirectoryInfo.FullName));
                }

                JObject index = JObject.Parse((new StreamReader(files[0].FullName)).ReadToEnd());

                await ListRanges(index, packages);
            }

            return packages;
        }

        public static void Dump(IDictionary<string, HashSet<string>> packages)
        {
            int count = 0;

            foreach (var package in packages)
            {
                Console.WriteLine(package.Key);

                foreach (var version in package.Value)
                {
                    count++;

                    Console.WriteLine("\t{0}", version);
                }
            }

            Console.WriteLine("total packages = {0}", count);
        }

        public static int Total(IDictionary<string, HashSet<string>> packages)
        {
            int total = 0;
            foreach (var package in packages)
            {
                total += package.Value.Count;
            }
            return total;
        }

        static void Compare(
            IDictionary<string, HashSet<string>> a,
            IDictionary<string, HashSet<string>> b,
            string nameOfA,
            string nameOfB)
        {
            foreach (var p1 in a)
            {
                foreach (string v1 in p1.Value)
                {
                    if (!b[p1.Key].Contains(v1))
                    {
                        Console.WriteLine("{0} {1} found in {2} missing in {3}", p1.Key, v1, nameOfA, nameOfB);
                    }
                }
            }
        }

        public static void Test0()
        {
            Console.WriteLine("IntegrityTests.Test0");

            //string path = "c:\\data\\site\\details";
            //string catalog = "http://localhost:8000/test/index.json";
            string path = "c:\\data\\site\\details";
            string catalog = "http://localhost:8000/automapper/index.json";

            IDictionary<string, HashSet<string>> packagesFromRegistration = GetRegistrationPackagesAsync(path).Result;

            PackageCollector collector = new PackageCollector(new Uri(catalog), null, 20);

            collector.Run().Wait();

            IDictionary<string, HashSet<string>> packagesFromCatalog = collector.Result;

            Compare(packagesFromRegistration, packagesFromCatalog, "registration", "catalog");
            Compare(packagesFromCatalog, packagesFromRegistration, "catalog", "registration");

            Console.WriteLine("Totals:");
            Console.WriteLine("    Registration: {0}", Total(packagesFromRegistration));
            Console.WriteLine("    Catalog:      {0}", Total(packagesFromCatalog));
        }

        public static void Test1()
        {
            Console.WriteLine("IntegrityTests.Test1");

            string catalog = "https://api.nuget.org/v3/catalog0/index.json";

            Func<HttpMessageHandler> handlerFunc = () =>
            {
                return new VerboseHandler();
            };

            DistinctPackageIdCollector collector = new DistinctPackageIdCollector(new Uri(catalog), handlerFunc, 20);

            collector.Run().Wait();

            HashSet<string> packagesFromCatalog = collector.Result;

            Console.WriteLine(packagesFromCatalog.Count);
        }

        public static void Test2()
        {
            Console.WriteLine("IntegrityTests.Test2");

            string catalog = "https://api.nuget.org/v3/catalog0/index.json";

            Func<HttpMessageHandler> handlerFunc = () =>
            {
                return new VerboseHandler();
            };

            FindCollector collector = new FindCollector(new Uri(catalog), handlerFunc, 20);

            collector.Run().Wait();

            Console.WriteLine(collector.Result.Count);

            foreach (string version in collector.Result["xunit.core"])
            {
                Console.WriteLine(version);
            }
        }

        public static void Test3()
        {
            Console.WriteLine("IntegrityTests.Test2");

            string catalog = "https://nugetdevstorage.blob.core.windows.net/catalog/index.json";

            Func<HttpMessageHandler> handlerFunc = () =>
            {
                return new VerboseHandler();
            };

            PrintCommitCollector collector = new PrintCommitCollector(new Uri(catalog), handlerFunc);

            collector.Run().Wait();
        }
    }
}
