using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using NuGet.Packaging;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CatalogTests
{
    class BuilderTests
    {
        public static async Task Test0Async()
        {
            //string nuspecs = @"c:\data\nuget\nuspecs";
            string nuspecs = @"c:\data\nuget\nuspecs";

            //Storage storage = new FileStorage("http://localhost:8000/full", @"c:\data\site\full");
            Storage storage = new FileStorage("http://localhost:8000/dotnetrdf", @"c:\data\site\dotnetrdf");

            AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage, 600);

            const int BatchSize = 200;
            int i = 0;

            int commitCount = 0;

            DirectoryInfo directoryInfo = new DirectoryInfo(nuspecs);
            //foreach (FileInfo fileInfo in directoryInfo.EnumerateFiles("*.xml"))
            foreach (FileInfo fileInfo in directoryInfo.EnumerateFiles("dotnetrdf.*.xml"))
            {
                writer.Add(new NuspecPackageCatalogItem(fileInfo.FullName));

                if (++i % BatchSize == 0)
                {
                    await writer.Commit(DateTime.UtcNow);

                    Console.WriteLine("commit number {0}", commitCount++);
                }
            }

            await writer.Commit(DateTime.UtcNow);

            Console.WriteLine("commit number {0}", commitCount++);
        }

        public static void Test0()
        {
            Console.WriteLine("BuilderTests.Test0");

            Test0Async().Wait();
        }

        public static async Task Test1Async()
        {
            string nuspecs = @"c:\data\nuget\nuspecs";

            Storage storage = new FileStorage("http://localhost:8000/full/", @"c:\data\site\full");

            AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage, 20);

            int total = 0;

            //int[] commitSize = { 50, 40, 25, 50, 10, 30, 40, 5, 400, 30, 10, 20, 40, 50, 90, 70, 50, 50, 50, 50, 60, 70 };
            int[] commitSize = { 
                20, 20, 20, 20, 20, 
                20, 20, 20, 20, 20, 
                //200, 200, 200, 200, 200, 
                //200, 200, 200, 200, 200, 
                //200, 200, 200, 200, 200, 
                //200, 200, 200, 200, 200,
                //200, 200, 200, 200, 200
            };
            int i = 0;

            int commitCount = 0;

            DirectoryInfo directoryInfo = new DirectoryInfo(nuspecs);
            foreach (FileInfo fileInfo in directoryInfo.EnumerateFiles("dotnetrdf.*.xml"))
            {
                if (commitCount == commitSize.Length)
                {
                    break;
                }

                writer.Add(new NuspecPackageCatalogItem(fileInfo.FullName));
                total++;

                if (++i == commitSize[commitCount])
                {
                    await writer.Commit(DateTime.UtcNow);

                    Console.WriteLine("commit number {0}", commitCount);

                    commitCount++;
                    i = 0;
                }
            }

            if (i > 0)
            {
                await writer.Commit(DateTime.UtcNow);
            }

            Console.WriteLine("total: {0}", total);
        }

        public static void Test1()
        {
            Console.WriteLine("BuilderTests.Test1");

            Test1Async().Wait();
        }

        public static async Task Test2Async()
        {
            IDictionary<string, string> packageHashLookup = LoadPackageHashLookup();
            IDictionary<string, DateTime> packageCreatedLookup = LoadPackageCreatedLookup();
            HashSet<string> packageExceptionLookup = LoadPackageExceptionLookup();

            string nupkgs = @"c:\data\nuget\gallery";

            //Storage storage = new FileStorage("http://localhost:8000/publish", @"c:\data\site\publish");
            //Storage storage = new FileStorage("http://localhost:8000/dotnetrdf", @"c:\data\site\dotnetrdf");
            //Storage storage = new FileStorage("http://localhost:8000/entityframework", @"c:\data\site\entityframework");

            StorageCredentials credentials = new StorageCredentials("", "");
            CloudStorageAccount account = new CloudStorageAccount(credentials, true);
            Storage storage = new AzureStorage(account, "ver37", "catalog");

            AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage, 600);

            const int BatchSize = 200;
            int i = 0;

            int commitCount = 0;

            DirectoryInfo directoryInfo = new DirectoryInfo(nupkgs);
            foreach (FileInfo fileInfo in directoryInfo.EnumerateFiles("*.nupkg"))
            //foreach (FileInfo fileInfo in directoryInfo.EnumerateFiles("dotnetrdf.*.nupkg"))
            //foreach (FileInfo fileInfo in directoryInfo.EnumerateFiles("entityframework.*.nupkg"))
            {
                if (i++ < 248000)
                {
                    continue;
                }

                Tuple<XDocument, IEnumerable<PackageEntry>, long, string> metadata = GetNupkgMetadata(fileInfo.FullName);

                if (metadata != null)
                {
                    string packageHash = packageHashLookup[fileInfo.Name];

                    DateTime packageCreated;
                    if (packageCreatedLookup.TryGetValue(fileInfo.Name, out packageCreated))
                    {
                        if (!packageExceptionLookup.Contains(fileInfo.Name))
                        {
                            writer.Add(new NuspecPackageCatalogItem(metadata.Item1, packageCreated, metadata.Item2, metadata.Item3, metadata.Item4));
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Skipping {0}", fileInfo.FullName);
                }

                if (i % BatchSize == 0)
                {
                    await writer.Commit(DateTime.UtcNow);

                    Console.WriteLine("commit number {0}", commitCount++);
                }
            }

            await writer.Commit(DateTime.UtcNow);

            Console.WriteLine("commit number {0}", commitCount++);
        }

        private static IEnumerable<string> GetSupportedFrameworks(string filename)
        {
            try
            {
                using (var stream = File.OpenRead(filename))
                {
                    ZipFileSystem zip = new ZipFileSystem(stream);

                    using (PackageReader reader = new PackageReader(zip))
                    {
                        ArtifactReader artifactReader = new ArtifactReader(reader);

                        return artifactReader.GetSupportedFrameworks();
                    }
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Failed to extract supported frameworks from {0} execption {1}", filename, e.Message);

                // default to any for errors
                return new string[] { "any" };
            }
        }

        static Tuple<XDocument, IEnumerable<PackageEntry>, long, string> GetNupkgMetadata(string filename)
        {
            try
            {
                using (Stream stream = new FileStream(filename, FileMode.Open))
                {
                    long packageFileSize = stream.Length;

                    string packageHash = GenerateHash(stream);

                    using (ZipArchive package = new ZipArchive(stream))
                    {
                        XDocument nuspec = Utils.GetNuspec(package);

                        if (nuspec == null)
                        {
                            throw new Exception(string.Format("Unable to find nuspec in {0}", filename));
                        }

                        IEnumerable<PackageEntry> entries = GetEntries(package);

                        return Tuple.Create(nuspec, entries, packageFileSize, packageHash);
                    }
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Failed to extract metadata from {0} execption {1}", filename, e.Message);
                return null;
            }
        }

        public static IDictionary<string, string> LoadPackageHashLookup()
        {
            string packageHashFile = @"c:\data\nuget\packageHash.txt";

            IDictionary<string, string> result = new Dictionary<string, string>();

            if (!File.Exists(packageHashFile))
            {
                return result;
            }

            using (TextReader reader = new StreamReader(packageHashFile))
            {
                for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
                {
                    int index = line.IndexOf(@"\");

                    string filename = line.Substring(0, index);
                    string hash = line.Substring(index + 1);

                    result.Add(filename, hash);
                }
            }

            return result;
        }

        public static IDictionary<string, DateTime> LoadPackageCreatedLookup()
        {
            string packageCreated = @"c:\data\nuget\packageCreated.txt";

            IDictionary<string, DateTime> result = new Dictionary<string, DateTime>();

            if (!File.Exists(packageCreated))
            {
                return result;
            }

            using (TextReader reader = new StreamReader(packageCreated))
            {
                for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
                {
                    int index = line.IndexOf(@"\");

                    string filename = line.Substring(0, index);
                    string s = line.Substring(index + 1);

                    DateTime dateTime = DateTime.Parse(s, null, DateTimeStyles.RoundtripKind);

                    result.Add(filename, dateTime);
                }
            }

            return result;
        }

        static HashSet<string> LoadPackageExceptionLookup()
        {
            string packageExceptions = @"c:\data\nuget\packageExceptions.txt";

            HashSet<string> result = new HashSet<string>();

            if (!File.Exists(packageExceptions))
            {
                return result;
            }

            using (TextReader reader = new StreamReader(packageExceptions))
            {
                for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
                {
                    result.Add(line);
                }
            }

            return result;
        }

        public static string GenerateHash(Stream stream)
        {
            byte[] buffer = new byte[stream.Length];
            MemoryStream memoryStream = new MemoryStream(buffer);
            stream.CopyTo(memoryStream);

            using (HashAlgorithm hashAlgorithm = HashAlgorithm.Create("SHA512"))
            {
                return Convert.ToBase64String(hashAlgorithm.ComputeHash(buffer));
            }
        }

        static IEnumerable<PackageEntry> GetEntries(ZipArchive package)
        {
            IList<PackageEntry> result = new List<PackageEntry>();

            foreach (ZipArchiveEntry entry in package.Entries)
            {
                if (entry.FullName.EndsWith("/.rels", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (entry.FullName.EndsWith("[Content_Types].xml", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (entry.FullName.EndsWith(".psmdcp", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(new PackageEntry(entry));
            }
            
            return result;
        }

        public static void Test2()
        {
            Console.WriteLine("BuilderTests.Test2");

            Test2Async().Wait();
        }

        public static async Task Test3Async()
        {
            IDictionary<string, string> packageHashLookup = LoadPackageHashLookup();
            HashSet<string> packageExceptionLookup = LoadPackageExceptionLookup();

            string nupkgs = @"c:\data\nuget\gallery\";

            Storage storage = new FileStorage("http://localhost:8000/ordered", @"c:\data\site\ordered");

            //StorageCredentials credentials = new StorageCredentials("", "");
            //CloudStorageAccount account = new CloudStorageAccount(credentials, true);
            //Storage storage = new AzureStorage(account, "ver38", "catalog");

            storage.Verbose = true;

            AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage, 600);

            const int BatchSize = 200;
            int i = 0;

            int commitCount = 0;

            IDictionary<string, DateTime> packageCreated = LoadPackageCreatedLookup();

            DateTime lastCreated = (await PackageCatalog.ReadCommitMetadata(writer)).Item1 ?? DateTime.MinValue;

            foreach (KeyValuePair<string, DateTime> entry in packageCreated)
            {
                if (entry.Value <= lastCreated)
                {
                    continue;
                }

                if (packageExceptionLookup.Contains(entry.Key))
                {
                    continue;
                }

                FileInfo fileInfo = new FileInfo(nupkgs + entry.Key);

                if (fileInfo.Exists)
                {
                    var supportedFrameworks = GetSupportedFrameworks(fileInfo.FullName);

                    string packageHash = packageHashLookup[fileInfo.Name];
                    Tuple<XDocument, IEnumerable<PackageEntry>, long, string> metadata = GetNupkgMetadata(fileInfo.FullName);
                    writer.Add(new NuspecPackageCatalogItem(metadata.Item1, entry.Value, metadata.Item2, metadata.Item3, metadata.Item4, supportedFrameworks));

                    lastCreated = entry.Value;

                    if (++i % BatchSize == 0)
                    {
                        await writer.Commit(DateTime.UtcNow, PackageCatalog.CreateCommitMetadata(writer.RootUri, lastCreated, null));

                        Console.WriteLine("commit number {0}", commitCount++);
                    }
                }
            }

            await writer.Commit(DateTime.UtcNow, PackageCatalog.CreateCommitMetadata(writer.RootUri, lastCreated, null));

            Console.WriteLine("commit number {0}", commitCount++);
        }

        public static void Test3()
        {
            Console.WriteLine("BuilderTests.Test3");

            Test3Async().Wait();
        }
    }
}
