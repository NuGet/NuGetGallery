using Catalog.Maintenance;
using Catalog.Persistence;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CatalogTests
{
    class BuilderTests
    {
        public static async Task Test0Async()
        {
            string nuspecs = @"c:\data\nuspecs";

            Storage storage = new FileStorage
            {
                Path = @"c:\data\site\pub",
                Container = "pub",
                BaseAddress = "http://localhost:8000/"
            };

            //Storage storage = new AzureStorage
            //{
            //    AccountName = "nuget3",
            //    AccountKey = "",
            //    Container = "pub",
            //    BaseAddress = "http://nuget3.blob.core.windows.net"
            //};

            CatalogContext context = new CatalogContext();

            CatalogWriter writer = new CatalogWriter(storage, context, 1000);

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
            }

            await writer.Commit(DateTime.Now);

            Console.WriteLine("commit number {0}", commitCount++);
        }

        public static void Test0()
        {
            Test0Async().Wait();
        }

        public static async Task Test1Async()
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

        public static void Test1()
        {
            Test1Async().Wait();
        }

        public static void Test2()
        {
        }
    }
}
