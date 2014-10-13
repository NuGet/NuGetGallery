using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
    }
}
