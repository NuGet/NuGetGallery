using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using NuGet.Packaging;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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

            AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage, 15);

            const int BatchSize = 10;
            int i = 0;

            int commitCount = 0;

            DirectoryInfo directoryInfo = new DirectoryInfo(nuspecs);
            //foreach (FileInfo fileInfo in directoryInfo.EnumerateFiles("*.xml"))
            //foreach (FileInfo fileInfo in directoryInfo.EnumerateFiles("dotnetrdf.*.xml"))
            foreach (FileInfo fileInfo in directoryInfo.EnumerateFiles("entityframework.*.xml"))
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

        public static async Task Test3Async()
        {
            System.Net.ServicePointManager.DefaultConnectionLimit = 1024;
            IDictionary<string, string> packageHashLookup = LoadPackageHashLookup();
            HashSet<string> packageExceptionLookup = LoadPackageExceptionLookup();

            string nupkgs = @"c:\data\nuget\gallery\";

            Storage storage = new FileStorage("http://localhost:8000/ordered", @"c:\data\site\ordered");

            //StorageCredentials credentials = new StorageCredentials("", "");
            //CloudStorageAccount account = new CloudStorageAccount(credentials, true);
            //Storage storage = new AzureStorage(account, "ver38", "catalog");
            //storage.Verbose = true;

            AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage, 550);

            const int BatchSize = 64;
 
            int commitCount = 0;

            IDictionary<string, DateTime> packageCreated = LoadPackageCreatedLookup();

            DateTime lastCreated = (await PackageCatalog.ReadCommitMetadata(writer)).Item1 ?? DateTime.MinValue;

            ParallelOptions options = new ParallelOptions();
            options.MaxDegreeOfParallelism = 8;

            // filter by lastCreated here
            Queue<KeyValuePair<string, DateTime>> packageCreatedQueue = new Queue<KeyValuePair<string, DateTime>>(packageCreated.Where(p => p.Value > lastCreated && !packageExceptionLookup.Contains(p.Key)).OrderBy(p => p.Value));

            int completed = 0;
            Stopwatch runtime = new Stopwatch();
            runtime.Start();

            Task commitTask = null;
            var context = writer.Context;
            Uri rootUri = writer.RootUri;

            while (packageCreatedQueue.Count > 0)
            {
                List<KeyValuePair<string, DateTime>> batch = new List<KeyValuePair<string, DateTime>>();

                ConcurrentBag<CatalogItem> batchItems = new ConcurrentBag<CatalogItem>();

                while (batch.Count < BatchSize && packageCreatedQueue.Count > 0)
                {
                    completed++;
                    var packagePair = packageCreatedQueue.Dequeue();
                    lastCreated = packagePair.Value;
                    batch.Add(packagePair);
                }

                var commitTime = DateTime.UtcNow;

                Parallel.ForEach(batch, options, entry =>
                {
                    FileInfo fileInfo = new FileInfo(nupkgs + entry.Key);

                    if (fileInfo.Exists)
                    {
                        using (Stream stream = new FileStream(fileInfo.FullName, FileMode.Open))
                        {
                            string packageHash = null;
                            packageHashLookup.TryGetValue(fileInfo.Name, out packageHash);

                            CatalogItem item = Utils.CreateCatalogItem(stream, entry.Value, packageHash, fileInfo.FullName);
                            batchItems.Add(item);
                        }
                    }
                });

                if (commitTask != null)
                {
                    commitTask.Wait();
                }

                foreach (var item in batchItems)
                {
                    writer.Add(item);
                }

                commitTask = Task.Run(async () => await writer.Commit(commitTime, PackageCatalog.CreateCommitMetadata(writer.RootUri, lastCreated, null)));

                // stats
                double perPackage = runtime.Elapsed.TotalSeconds / (double)completed;
                DateTime finish = DateTime.Now.AddSeconds(perPackage * packageCreatedQueue.Count);

                Console.WriteLine("commit number {0} Completed: {1} Remaining: {2} Estimated Finish: {3}",
                    commitCount++,
                    completed,
                    packageCreatedQueue.Count,
                    finish.ToString("O"));
            }

            // wait for the final commit
            if (commitTask != null)
            {
                commitTask.Wait();
            }

            Console.WriteLine("Finished in: " + runtime.Elapsed);
        }

        public static void Test3()
        {
            Console.WriteLine("BuilderTests.Test3");

            Test3Async().Wait();
        }
    }
}
