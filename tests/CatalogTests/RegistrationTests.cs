using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Collecting;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Segmentation;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace CatalogTests
{
    class RegistrationTests
    {
        static async Task PrintAsync(Uri indexUri)
        {
            FileSystemEmulatorHandler handler = new FileSystemEmulatorHandler
            {
                BaseAddress = new Uri("http://localhost:8000"),
                RootFolder = @"c:\data\site",
                InnerHandler = new HttpClientHandler()
            };

            HttpClient client = new HttpClient(handler);

            HttpResponseMessage indexResponse = await client.GetAsync(indexUri);
            string indexJson = await indexResponse.Content.ReadAsStringAsync();
            JObject index = JObject.Parse(indexJson);

            SortedList<string, Uri> segments = new SortedList<string, Uri>();

            foreach (JObject entry in index["entry"])
            {
                segments.Add(entry["lowest"].ToString(), entry["url"].ToObject<Uri>());
            }

            foreach (var item in segments)
            {
                HttpResponseMessage segmentResponse = await client.GetAsync(item.Value);
                string segmentJson = await segmentResponse.Content.ReadAsStringAsync();
                JObject segment = JObject.Parse(segmentJson);

                SortedList<string, JObject> packages = new SortedList<string, JObject>();

                foreach (JObject entry in segment["entry"])
                {
                    packages.Add(entry["id"].ToString() + "." + entry["version"].ToString(), entry);
                }

                foreach (var package in packages)
                {
                    string description = package.Value["description"].ToString();
                    description = description.Substring(0, Math.Min(description.Length, 25)) + "...";

                    Console.WriteLine("{0}\t{1}\t{2}", package.Value["id"], package.Value["version"], description);
                }
            }
        }

        static async Task CountAsync(Uri indexUri)
        {
            FileSystemEmulatorHandler handler = new FileSystemEmulatorHandler
            {
                BaseAddress = new Uri("http://localhost:8000"),
                RootFolder = @"c:\data\site",
                InnerHandler = new HttpClientHandler()
            };

            HttpClient client = new HttpClient(handler);

            HttpResponseMessage indexResponse = await client.GetAsync(indexUri);
            string indexJson = await indexResponse.Content.ReadAsStringAsync();
            JObject index = JObject.Parse(indexJson);

            SortedList<string, Uri> segments = new SortedList<string, Uri>();

            int count1 = 0;

            foreach (JObject entry in index["entry"])
            {
                count1 += entry["count"].ToObject<int>();

                segments.Add(entry["lowest"].ToString(), entry["url"].ToObject<Uri>());
            }

            int count2 = 0;

            foreach (var item in segments)
            {
                HttpResponseMessage segmentResponse = await client.GetAsync(item.Value);
                string segmentJson = await segmentResponse.Content.ReadAsStringAsync();
                JObject segment = JObject.Parse(segmentJson);

                count2 += ((JArray)segment["entry"]).Count;
            }

            Console.WriteLine("{0} {1}", count1, count2);
        }

        static async Task Test2Async()
        {
            Storage storage = new FileStorage("http://localhost:8000/test/", @"c:\data\site\test");

            SegmentWriter writer = new SegmentWriter(storage, "registration", 4, true);

            writer.Add(new IdKeyEntry("a", "1.0.0", "A", "http://tempuri.org/registration"));
            writer.Add(new IdKeyEntry("b", "1.0.0", "B", "http://tempuri.org/registration"));
            writer.Add(new IdKeyEntry("c", "1.0.0", "C", "http://tempuri.org/registration"));
            writer.Add(new IdKeyEntry("d", "1.0.0", "D", "http://tempuri.org/registration"));
            writer.Add(new IdKeyEntry("e", "1.0.0", "E", "http://tempuri.org/registration"));
            writer.Add(new IdKeyEntry("f", "1.0.0", "F", "http://tempuri.org/registration"));
            writer.Add(new IdKeyEntry("g", "1.0.0", "G", "http://tempuri.org/registration"));
            writer.Add(new IdKeyEntry("h", "1.0.0", "H", "http://tempuri.org/registration"));
            writer.Add(new IdKeyEntry("i", "1.0.0", "I", "http://tempuri.org/registration"));
            writer.Add(new IdKeyEntry("j", "1.0.0", "J", "http://tempuri.org/registration"));
            writer.Add(new IdKeyEntry("k", "1.0.0", "K", "http://tempuri.org/registration"));

            await writer.Commit();

            SegmentWriter writer2 = new SegmentWriter(storage, "registration", 4, true);

            writer2.Add(new IdKeyEntry("bb", "1.0.0", "BB", "http://tempuri.org/registration"));
            writer2.Add(new IdKeyEntry("dd", "1.0.0", "DD", "http://tempuri.org/registration"));

            await writer2.Commit();

            SegmentWriter writer3 = new SegmentWriter(storage, "registration", 4, true);

            writer3.Add(new IdKeyEntry("aa", "1.0.0", "AA", "http://tempuri.org/registration"));
            writer3.Add(new IdKeyEntry("ab", "1.0.0", "AB", "http://tempuri.org/registration"));
            writer3.Add(new IdKeyEntry("ac", "1.0.0", "AC", "http://tempuri.org/registration"));
            writer3.Add(new IdKeyEntry("ad", "1.0.0", "AD", "http://tempuri.org/registration"));
            writer3.Add(new IdKeyEntry("ae", "1.0.0", "AE", "http://tempuri.org/registration"));
            writer3.Add(new IdKeyEntry("af", "1.0.0", "AF", "http://tempuri.org/registration"));
            writer3.Add(new IdKeyEntry("ag", "1.0.0", "AG", "http://tempuri.org/registration"));
            writer3.Add(new IdKeyEntry("ah", "1.0.0", "AH", "http://tempuri.org/registration"));

            await writer3.Commit();

            SegmentWriter writer4 = new SegmentWriter(storage, "registration", 4, true);

            writer4.Add(new IdKeyEntry("jj", "1.0.0", "JJ", "http://tempuri.org/registration"));

            await writer4.Commit();

            await PrintAsync(new Uri("http://localhost:8000/test/registration/segment_index.json"));
        }

        public static void Test2()
        {
            Test2Async().Wait();
        }

        static async Task Test3Async()
        {
            Storage storage = new FileStorage("http://localhost:8000/test/", @"c:\data\site\test");

            storage.Verbose = true;

            SegmentWriter writer = new SegmentWriter(storage, "registration", 3, true);

            writer.Add(new IdVersionKeyEntry("a", "1.0.0", "A1", "http://tempuri.org/registration"));
            writer.Add(new IdVersionKeyEntry("a", "2.0.0", "A2", "http://tempuri.org/registration"));
            writer.Add(new IdVersionKeyEntry("a", "3.0.0", "A3", "http://tempuri.org/registration"));
            writer.Add(new IdVersionKeyEntry("b", "1.0.0", "B1", "http://tempuri.org/registration"));

            await writer.Commit();

            SegmentWriter writer2 = new SegmentWriter(storage, "registration", 3, true);

            writer2.Add(new IdVersionKeyEntry("a", "4.0.0", "A4", "http://tempuri.org/registration"));
            writer2.Add(new IdVersionKeyEntry("b", "2.0.0", "B2", "http://tempuri.org/registration"));

            await writer2.Commit();

            await PrintAsync(new Uri("http://localhost:8000/test/registration/segment_index.json"));
        }

        public static void Test3()
        {
            Test3Async().Wait();
        }

        static async Task CreateRegistrationAsync(Storage storage, string connectionString, string sql, Func<string, string, string, Entry> factory)
        {
            SegmentWriter writer = new SegmentWriter(storage, "registration", 1000, true);

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand(sql, connection);

                SqlDataReader reader = command.ExecuteReader();

                int count = 1;

                //int i = 1;

                while (reader.Read())
                {
                    string id = reader.GetString(0);
                    string version = reader.GetString(1);
                    string description = reader.GetString(2);

                    try
                    {
                        Console.WriteLine("add({0},{1})", id, version);

                        writer.Add(factory(id, version, description));

                        //if (i++ % 1000 == 0)
                        //{
                        //    Console.WriteLine("commit {0}", count++);
                        //    await writer.Commit();
                        //}
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("exception on id: {0})", id);
                        //throw;
                    }
                }

                if (writer.ReadyCount > 0)
                {
                    Console.WriteLine("commit {0}", count++);

                    await writer.Commit();
                }
            }
        }

        static async Task Test4Async()
        {
            Storage storage = new FileStorage("http://localhost:8000/test/", @"c:\data\site\test");

            storage.Verbose = true;

            string islateststable = @"
                SELECT PackageRegistrations.[Id], Packages.[NormalizedVersion], Packages.[Description] 
                FROM Packages
                INNER JOIN PackageRegistrations ON Packages.PackageRegistrationKey = PackageRegistrations.[Key]
                WHERE Packages.Listed = 1
                    AND Packages.IsLatestStable = 1
                ORDER BY PackageRegistrations.[Id], Packages.[NormalizedVersion]
            ";

            string connectionString = (new StreamReader(@"c:\data\config.txt")).ReadToEnd();

            await CreateRegistrationAsync(storage, connectionString, islateststable, 
                (id, version, description) => new IdKeyEntry(id, version, description, "http://tempuri.org/registration"));
        }

        public static void Test4()
        {
            Test4Async().Wait();
        }

        static async Task Test5Async()
        {
            Storage storage = new FileStorage("http://localhost:8000/test/", @"c:\data\site\test");

            storage.Verbose = true;

            string allversions = @"
                SELECT PackageRegistrations.[Id], Packages.[NormalizedVersion], Packages.[Description] 
                FROM Packages
                INNER JOIN PackageRegistrations ON Packages.PackageRegistrationKey = PackageRegistrations.[Key]
                WHERE Packages.Listed = 1
                ORDER BY PackageRegistrations.[Id], Packages.[NormalizedVersion]
            ";

            string connectionString = (new StreamReader(@"c:\data\config.txt")).ReadToEnd();

            await CreateRegistrationAsync(storage, connectionString, allversions,
                (id, version, description) => new IdVersionKeyEntry(id, version, description, "http://tempuri.org/registration"));
        }

        public static void Test5()
        {
            Test5Async().Wait();
        }

        public static void Test6()
        {
            CountAsync(new Uri("http://localhost:8000/test/registration/segment_index.json")).Wait();
        }

        public static async Task Test7Async()
        {
            //  destination - we will build the segmentation into here

            Storage storage = new FileStorage("http://localhost:8000/test/", @"c:\data\site\test");

            //  source - we will read the data from here

            FileSystemEmulatorHandler handler = new FileSystemEmulatorHandler
            {
                BaseAddress = new Uri("http://localhost:8000"),
                RootFolder = @"c:\data\site",
                InnerHandler = new HttpClientHandler()
            };

            string registrationBaseAddress = "http://tempuri.org/registration";

            SegmentCollector collector = new SegmentCollector(200, storage, registrationBaseAddress);

            await collector.Run(new Uri("http://localhost:8000/full/index.json"), DateTime.MinValue, handler);

            await CountAsync(new Uri("http://localhost:8000/test/allversions/segment_index.json"));

            await PrintAsync(new Uri("http://localhost:8000/test/allversions/segment_index.json"));
        }

        public static void Test7()
        {
            Test7Async().Wait();
        }
    }
}
