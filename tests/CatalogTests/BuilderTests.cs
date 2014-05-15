using Catalog;
using Catalog.Maintenance;
using Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatalogTests
{
    class BuilderTests
    {
        static void BatchUpload(Storage storage, string ownerId, List<Tuple<string, string>> batch, DateTime published)
        {
            foreach (Tuple<string, string> t in batch)
            {
                Console.WriteLine(t.Item1);
            }

            LocalPackageHandle[] handles = batch.Select((item) => new LocalPackageHandle(new List<string>{ownerId}, item.Item1, item.Item2, published)).ToArray();

            Processor.Upload(handles, storage).Wait();

            Console.WriteLine("...uploaded");
        }

        static List<Tuple<string, string>> GatherPackageList(string paths)
        {
            List<Tuple<string, string>> packages = new List<Tuple<string, string>>();

            foreach (string path in paths.Split(';'))
            {
                DirectoryInfo nupkgs = new DirectoryInfo(path);
                foreach (DirectoryInfo registration in nupkgs.EnumerateDirectories())
                {
                    string registrationId = registration.Name.ToLowerInvariant();

                    foreach (FileInfo nupkg in registration.EnumerateFiles("*.nupkg"))
                    {
                        packages.Add(new Tuple<string, string>(registrationId, nupkg.FullName));
                    }
                }
            }

            return packages;
        }

        public static void Test0()
        {
            Storage storage = new FileStorage
            {
                Path = @"c:\data\site\pub",
                Container = "pub",
                BaseAddress = "http://localhost:8000"
            };

            string ownerId = "microsoft";
            string path = @"c:\data\nupkgs;c:\data\nupkgs2;c:\data\nupkgs3;c:\data\nupkgs4";

            const int BatchSize = 100;

            DateTime before = DateTime.Now;

            List<Tuple<string, string>> packages = GatherPackageList(path);

            List<Tuple<string, string>> batch = new List<Tuple<string, string>>();
            foreach (Tuple<string, string> item in packages)
            {
                batch.Add(item);

                if (batch.Count == BatchSize)
                {
                    BatchUpload(storage, ownerId, batch, DateTime.Now);
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
            {
                BatchUpload(storage, ownerId, batch, DateTime.Now);
            }

            DateTime after = DateTime.Now;

            Console.WriteLine("{0} seconds {1} packages", (after - before).TotalSeconds, packages.Count);

            if (storage is FileStorage)
            {
                Console.WriteLine("save: {0} load: {1}", ((FileStorage)storage).SaveCount, ((FileStorage)storage).LoadCount);
            }
        }

        public static void Test1()
        {
            Storage storage = new FileStorage
            {
                Path = @"c:\data\site\pub",
                Container = "pub",
                BaseAddress = "http://localhost:8000"
            };

            LocalPackageHandle[] handles = new LocalPackageHandle[0];
            Processor.Upload(handles, storage).Wait();
        }

        public static void Test2()
        {
            //Storage storage = new FileStorage
            //{
            //    Path = @"c:\data\site\pub",
            //    Container = "pub",
            //    BaseAddress = "http://localhost:8000"
            //};

            string accountName = "nuget3";
            string accountKey = "";
            string connectionString = string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", accountName, accountKey);
            Storage storage = new AzureStorage
            {
                ConnectionString = connectionString,
                Container = "pub",
                BaseAddress = "http://nuget3.blob.core.windows.net"
            };

            string ownerId = "microsoft";
            string path = @"c:\data\nupkgs;c:\data\nupkgs2;c:\data\nupkgs3;c:\data\nupkgs4";

            const int BatchSize = 100;

            DateTime before = DateTime.Now;

            List<Tuple<string, string>> packages = GatherPackageList(path);

            List<Tuple<string, string>> batch = new List<Tuple<string, string>>();
            foreach (Tuple<string, string> item in packages)
            {
                batch.Add(item);

                if (batch.Count == BatchSize)
                {
                    BatchUpload(storage, ownerId, batch, DateTime.Now);
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
            {
                BatchUpload(storage, ownerId, batch, DateTime.Now);
            }

            DateTime after = DateTime.Now;

            Console.WriteLine("{0} seconds {1} packages", (after - before).TotalSeconds, packages.Count);

            if (storage is FileStorage)
            {
                Console.WriteLine("save: {0} load: {1}", ((FileStorage)storage).SaveCount, ((FileStorage)storage).LoadCount);
            }
        }

        public static async Task Test3Async()
        {
            string nuspecs = @"c:\data\nuspecs";

            //Storage storage = new FileStorage
            //{
            //    Path = @"c:\data\site\full5",
            //    Container = "full5",
            //    BaseAddress = "http://localhost:8000/"
            //};

            Storage storage = new AzureStorage
            {
                AccountName = "nuget3",
                AccountKey = "",
                Container = "pub",
                BaseAddress = "http://nuget3.blob.core.windows.net"
            };

            CatalogContext context = new CatalogContext();

            CatalogWriter writer = new CatalogWriter(storage, context);

            const int BatchSize = 1000;
            int i = 0;

            int commitCount = 0;

            DirectoryInfo directoryInfo = new DirectoryInfo(nuspecs);
            foreach (FileInfo fileInfo in directoryInfo.EnumerateFiles("*.xml"))
            {
                writer.Add(new NuspecPackageCatalogItem(fileInfo));

                if (++i % BatchSize == 0)
                {
                    await writer.Commit(DateTime.Now);

                    Console.WriteLine("commit number {0}", commitCount++);
                }

                //if (commitCount == 20)
                //{
                //    break;
                //}
            }

            await writer.Commit(DateTime.Now);

            Console.WriteLine("commit number {0}", commitCount++);
        }

        public static void Test3()
        {
            Test3Async().Wait();
        }

        public static async Task Test4Async()
        {
            string nuspecs = @"c:\data\nuspecs";

            Storage storage = new FileStorage
            {
                Path = @"c:\data\site\full5",
                Container = "full5",
                BaseAddress = "http://localhost:8000/"
            };

            CatalogContext context = new CatalogContext();

            CatalogWriter writer = new CatalogWriter(storage, context);

            int total = 0;

            int[] commitSize = { 50, 40, 25, 50, 10, 30, 40, 5, 400, 30, 10 };
            int i = 0;

            int commitCount = 0;

            DirectoryInfo directoryInfo = new DirectoryInfo(nuspecs);
            foreach (FileInfo fileInfo in directoryInfo.EnumerateFiles("*.xml"))
            {
                if (commitCount == commitSize.Length)
                {
                    break;
                }

                writer.Add(new NuspecPackageCatalogItem(fileInfo));
                total++;

                if (++i == commitSize[commitCount])
                {
                    await writer.Commit(DateTime.Now);

                    Console.WriteLine("commit number {0}", commitCount);

                    commitCount++;
                    i = 0;
                }
            }

            Console.WriteLine("total: {0}", total);
        }

        public static void Test4()
        {
            Test4Async().Wait();
        }
    }
}
