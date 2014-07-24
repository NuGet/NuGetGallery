using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Registration;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;

namespace CatalogTests
{
    class RegistrationTests
    {
        static Storage CreateStorage(string name)
        {
            Storage storage = new FileStorage("http://localhost:8000/" + name + "/", @"c:\data\site\" + name);

            //CloudStorageAccount account = new CloudStorageAccount(new StorageCredentials(...), false);
            //Storage storage = new AzureStorage(account, name);

            return storage;
        }

        /*
        static async Task CreateRegistrationAsync(Storage storage, string resolverBaseAddress, string connectionString, string sql)
        {
            await CreateRegistrationAsync(storage, resolverBaseAddress, connectionString, sql, (entry) => entry.Id);
        }

        static async Task CreateRegistrationAsync(Storage storage, string resolverBaseAddress, string connectionString, string sql, Func<Entry, string> key)
        {
            RegistrationBuilder builder = new RegistrationBuilder(storage, "registration", 1000, true);

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand(sql, connection);

                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    string id = reader.GetString(0);
                    string version = reader.GetString(1);
                    string description = reader.GetString(2);

                    try
                    {
                        builder.Add(new Entry
                        {
                            Uri = new Uri(resolverBaseAddress + id.ToLowerInvariant() + ".json#" + version),
                            Id = id,
                            Version = version,
                            Description = description
                        },
                        key);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("exception on id: {0})", id);
                        //throw;
                    }
                }
            }

            await builder.Commit();
        }

        public static async Task Test0Async()
        {
            string resolverBaseAddress = "http://nugetdev0.blob.core.windows.net/cdn-public/v3/resolver/";
            string connectionString = "...";

            string islatest = @"
                        SELECT PackageRegistrations.[Id], Packages.[NormalizedVersion], Packages.[Description] 
                        FROM Packages
                        INNER JOIN PackageRegistrations ON Packages.PackageRegistrationKey = PackageRegistrations.[Key]
                        WHERE Packages.Listed = 1
                          AND Packages.IsLatest = 1
                    ";

            string islateststable = @"
                        SELECT PackageRegistrations.[Id], Packages.[NormalizedVersion], Packages.[Description] 
                        FROM Packages
                        INNER JOIN PackageRegistrations ON Packages.PackageRegistrationKey = PackageRegistrations.[Key]
                        WHERE Packages.Listed = 1
                          AND Packages.IsLatestStable = 1
                    ";

            string allversions = @"
                        SELECT PackageRegistrations.[Id], Packages.[NormalizedVersion], Packages.[Description] 
                        FROM Packages
                        INNER JOIN PackageRegistrations ON Packages.PackageRegistrationKey = PackageRegistrations.[Key]
                        WHERE Packages.Listed = 1
                    ";

            await CreateRegistrationAsync(CreateStorage("islatest"), resolverBaseAddress, connectionString, islatest);
            await CreateRegistrationAsync(CreateStorage("islateststable"), resolverBaseAddress, connectionString, islateststable);
            await CreateRegistrationAsync(CreateStorage("allversions"), resolverBaseAddress, connectionString, allversions);
        }

        public static void Test0()
        {
            Test0Async().Wait();
        }
        */

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

        static async Task Test2Async()
        {
            Storage storage = CreateStorage("test");

            SegmentWriter writer = new SegmentWriter(storage, "registration", 4, true);

            writer.Add(new TestSegmentEntry("a", "1.0.0", "A"));
            writer.Add(new TestSegmentEntry("b", "1.0.0", "B"));
            writer.Add(new TestSegmentEntry("c", "1.0.0", "C"));
            writer.Add(new TestSegmentEntry("d", "1.0.0", "D"));
            writer.Add(new TestSegmentEntry("e", "1.0.0", "E"));
            writer.Add(new TestSegmentEntry("f", "1.0.0", "F"));
            writer.Add(new TestSegmentEntry("g", "1.0.0", "G"));
            writer.Add(new TestSegmentEntry("h", "1.0.0", "H"));
            writer.Add(new TestSegmentEntry("i", "1.0.0", "I"));
            writer.Add(new TestSegmentEntry("j", "1.0.0", "J"));
            writer.Add(new TestSegmentEntry("k", "1.0.0", "K"));

            await writer.Commit();

            //SegmentWriter writer2 = new SegmentWriter(storage, "registration", 4, true);

            //writer2.Add(new TestSegmentEntry("bb", "1.0.0", "BB"));
            //writer2.Add(new TestSegmentEntry("dd", "1.0.0", "DD"));

            //await writer2.Commit();

            SegmentWriter writer3 = new SegmentWriter(storage, "registration", 4, true);

            writer3.Add(new TestSegmentEntry("aa", "1.0.0", "AA"));
            writer3.Add(new TestSegmentEntry("ab", "1.0.0", "AB"));
            writer3.Add(new TestSegmentEntry("ac", "1.0.0", "AC"));
            writer3.Add(new TestSegmentEntry("ad", "1.0.0", "AD"));
            writer3.Add(new TestSegmentEntry("ae", "1.0.0", "AE"));
            writer3.Add(new TestSegmentEntry("af", "1.0.0", "AF"));
            writer3.Add(new TestSegmentEntry("ag", "1.0.0", "AG"));
            writer3.Add(new TestSegmentEntry("ah", "1.0.0", "AH"));

            await writer3.Commit();

            SegmentWriter writer4 = new SegmentWriter(storage, "registration", 4, true);

            writer4.Add(new TestSegmentEntry("jj", "1.0.0", "JJ"));

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

            SegmentWriter writer = new SegmentWriter(storage, "registration", 10, true);

            writer.Add(new TestSegmentEntry("a", "1.0.0", "A"));
            writer.Add(new TestSegmentEntry("b", "1.0.0", "B"));
            writer.Add(new TestSegmentEntry("c", "1.0.0", "C"));
            writer.Add(new TestSegmentEntry("d", "1.0.0", "D"));

            await writer.Commit();

            SegmentWriter writer2 = new SegmentWriter(storage, "registration", 10, true);

            writer2.Add(new TestSegmentEntry("aa", "1.0.0", "AA"));

            await writer2.Commit();

            await PrintAsync(new Uri("http://localhost:8000/test/registration/segment_index.json"));
        }

        public static void Test3()
        {
            Test3Async().Wait();
        }

        class TestSegmentEntry : SegmentEntry
        {
            string _key;

            public TestSegmentEntry(string id, string version, string description)
            {
                _key = id;

                Id = id;
                Version = version;
                Description = description;
            }

            public override string Key
            {
                get { return _key; }
            }

            public string Id { get; set; }
            public string Version { get; set; }
            public string Description { get; set; }

            public override IGraph GetSegmentContent(Uri uri)
            {
                IGraph graph = new Graph();

                graph.NamespaceMap.AddNamespace("nuget", new Uri("http://schema.nuget.org/schema#"));

                INode subject = graph.CreateUriNode(uri);

                graph.Assert(subject, graph.CreateUriNode("nuget:id"), graph.CreateLiteralNode(Id));
                graph.Assert(subject, graph.CreateUriNode("nuget:version"), graph.CreateLiteralNode(Version));
                graph.Assert(subject, graph.CreateUriNode("nuget:description"), graph.CreateLiteralNode(Description));

                return graph;
            }
        }
    }
}
