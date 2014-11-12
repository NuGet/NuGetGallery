using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatalogTests
{
    public class GalleryCompareTests
    {
        // These need to be set by the caller!
        public static string GalleryDBConnectionString = null;
        public static string StorageConnectionString = null;

        public static void Test0(string[] args)
        {
            Test0Async(args).Wait();
        }

        public static async Task Test0Async(string[] args)
        {
            if (GalleryDBConnectionString == null || StorageConnectionString == null)
            {
                throw new Exception("Set the connection strings first!");
            }

            GetMissing();
        }

        public static HashSet<PackageEntry> GetGalleryPackageEntries()
        {
            HashSet<PackageEntry> entries = new HashSet<PackageEntry>(PackageEntry.Comparer);

            var packages = GetGalleryPackages();

            foreach (string id in packages.Keys)
            {
                foreach (string version in packages[id])
                {
                    entries.Add(new PackageEntry(id, version));
                }
            }

            return entries;
        }

        public static Dictionary<string, List<string>> GetGalleryPackages()
        {

            FileInfo file = new FileInfo("gallerypackages.txt");

            if (!file.Exists)
            {
                Console.WriteLine("Getting gallery packages.");

                string sql = @"SELECT [Id], [NormalizedVersion], [Created]
              FROM [dbo].[PackageRegistrations] join Packages on PackageRegistrations.[Key] = Packages.[PackageRegistrationKey]";

                string connectionString = GalleryDBConnectionString;

                SqlConnection connection = new SqlConnection(connectionString);

                connection.Open();
                SqlCommand command = new SqlCommand(sql, connection);
                SqlDataReader reader = command.ExecuteReader(CommandBehavior.CloseConnection);

                using (StreamWriter writer = new StreamWriter(file.FullName))
                {
                    if (reader != null)
                    {
                        while (reader.Read())
                        {
                            string id = reader["Id"].ToString().ToLowerInvariant();
                            string version = reader["NormalizedVersion"].ToString().ToLowerInvariant();
                            string created = reader["Created"].ToString();

                            string s = String.Format("{0}|{1}|{2}", id, version, created).ToLowerInvariant();
                            writer.WriteLine(s);
                            Console.WriteLine(s);
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("Loading gallery packages from file: " + file.FullName);
            }

            Dictionary<string, List<string>> packages = new Dictionary<string, List<string>>();
            using (StreamReader streamReader = new StreamReader(file.FullName))
            {

                while (!streamReader.EndOfStream)
                {
                    string[] line = streamReader.ReadLine().Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    string id = line[0];
                    string version = line[1];

                    List<string> versions = null;
                    if (!packages.TryGetValue(id, out versions))
                    {
                        versions = new List<string>();
                        packages.Add(id, versions);
                    }

                    versions.Add(version);
                }
            }

            return packages;
        }


        private static void GetMissing()
        {
            using (StreamWriter writer = new StreamWriter("log.txt", false))
            {
                var account = CloudStorageAccount.Parse(StorageConnectionString);
                TransHttpClient client = new TransHttpClient(account, "https://az320820.vo.msecnd.net/");

                var blobClient = account.CreateCloudBlobClient();
                var regContainer = blobClient.GetContainerReference("registrations-0");

                Uri catalogIndex = new Uri("https://az320820.vo.msecnd.net/catalog-0/index.json");

                CatalogIndexReader reader = new CatalogIndexReader(catalogIndex, client);

                var task = reader.GetEntries();


                HashSet<string> allRegUrls = new HashSet<string>();

                FileInfo regFile = new FileInfo("registrations.txt");
                if (!regFile.Exists)
                {
                    using (StreamWriter regWriter = new StreamWriter(regFile.FullName))
                    {
                        foreach (var regUrl in regContainer.ListBlobs(null, true).Select(b => b.Uri.LocalPath))
                        {
                            regWriter.WriteLine(regUrl);
                        }
                    }
                }

                using (StreamReader regReader = new StreamReader(regFile.FullName))
                {
                    while (!regReader.EndOfStream)
                    {
                        allRegUrls.Add(regReader.ReadLine());
                    }
                }


                var galleryPackages = GetGalleryPackageEntries();

                task.Wait();

                var entries = task.Result;

                var catalogPackages = new HashSet<PackageEntry>(entries.Select(e => new PackageEntry(e.Id, e.Version.ToNormalizedString())), PackageEntry.Comparer);

                HashSet<string> regIndexesById = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<PackageEntry> regPackages = new HashSet<PackageEntry>(PackageEntry.Comparer);

                foreach (string[] parts in allRegUrls.Select(u => u.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries)))
                {
                    if (parts.Length == 3)
                    {
                        string id = parts[1].ToLowerInvariant();
                        string file = parts[2].Replace(".json", string.Empty);

                        if (StringComparer.OrdinalIgnoreCase.Equals("index", file))
                        {
                            regIndexesById.Add(id);
                        }

                        regPackages.Add(new PackageEntry(id, file));
                    }
                }

                var catalogMissing = galleryPackages.Except(catalogPackages);
                var neededRegIndexes = new HashSet<string>(galleryPackages.Select(e => e.Id), StringComparer.OrdinalIgnoreCase);

                writer.WriteLine("Gallery => Catalog");
                LogDifferences(writer, galleryPackages, catalogPackages);

                writer.WriteLine("Gallery => Registrations");
                LogDifferences(writer, galleryPackages, regPackages);

                writer.WriteLine("Gallery => Registration Index");
                LogDifferences(writer, neededRegIndexes, regIndexesById);
            }
        }


        private static void LogDifferences(StreamWriter writer, HashSet<PackageEntry> main, HashSet<PackageEntry> subset)
        {
            var diff = main.Except(subset, PackageEntry.Comparer);
            var extra = subset.Except(subset, PackageEntry.Comparer);

            writer.WriteLine("Master: " + main.Count + " Subset: " + subset.Count);

            foreach (var e in diff)
            {
                writer.WriteLine("Missing: " + e.ToString());
            }

            foreach (var e in extra)
            {
                writer.WriteLine("Extra: " + e.ToString());
            }

            writer.WriteLine("-------------------");
        }

        private static void LogDifferences(StreamWriter writer, HashSet<string> main, HashSet<string> subset)
        {
            var diff = main.Except(subset, StringComparer.OrdinalIgnoreCase);
            var extra = subset.Except(subset, StringComparer.OrdinalIgnoreCase);

            writer.WriteLine("Master: " + main.Count + " Subset: " + subset.Count);

            foreach (var e in diff)
            {
                writer.WriteLine("Missing: " + e);
            }

            foreach (var e in extra)
            {
                writer.WriteLine("Extra: " + e);
            }

            writer.WriteLine("-------------------");
        }

        public class PackageEntry : IEquatable<PackageEntry>
        {
            public string Id { get; private set; }
            public string Version { get; private set; }

            public PackageEntry(string id, string version)
            {
                Id = id.ToLowerInvariant();
                Version = version.ToLowerInvariant();
            }

            public bool Equals(PackageEntry other)
            {
                return Compare(this, other);
            }

            public override string ToString()
            {
                return String.Format("{0} {1}", Id, Version);
            }

            public static bool Compare(PackageEntry x, PackageEntry y)
            {
                return StringComparer.OrdinalIgnoreCase.Equals(x.Id, y.Id) && StringComparer.OrdinalIgnoreCase.Equals(x.Version, y.Version);
            }

            public static IEqualityComparer<PackageEntry> Comparer
            {
                get
                {
                    return new PackageEntryComparer();
                }
            }

            public class PackageEntryComparer : IEqualityComparer<PackageEntry>
            {
                public bool Equals(PackageEntry x, PackageEntry y)
                {
                    return PackageEntry.Compare(x, y);
                }

                public int GetHashCode(PackageEntry obj)
                {
                    return obj.ToString().GetHashCode();
                }
            }
        }


    }
}
