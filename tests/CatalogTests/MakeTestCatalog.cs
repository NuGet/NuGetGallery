using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Test;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CatalogTests
{
    public class MakeTestCatalog
    {
        static IEnumerable<string> GetInitialIdList(int max)
        {
            IList<string> result = new List<string>();

            using (StreamReader reader = new StreamReader("Top1000.txt"))
            {
                int i=0;

                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    result.Add(line.ToLowerInvariant());

                    if (++i == max)
                    {
                        break;
                    }
                }
            }

            return result;
        }

        static string GetIdFromFile(string fullName)
        {
            XDocument nuspec = XDocument.Load(fullName);
            foreach (XElement descendents in nuspec.Descendants())
            {
                if (descendents.Name.LocalName == "id")
                {
                    return descendents.Value.ToLowerInvariant();
                }
            }
            return string.Empty;
        }

        static IEnumerable<string> GetFileList(string path, IEnumerable<string> ids)
        {
            HashSet<string> result = new HashSet<string>();

            foreach (string id in ids)
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(path);
                foreach (FileInfo fileInfo in directoryInfo.EnumerateFiles(id + ".*.xml"))
                {
                    string idFromFile = GetIdFromFile(fileInfo.FullName);

                    if (idFromFile == id)
                    {
                        result.Add(fileInfo.FullName);
                    }
                }
            }

            return result;
        }

        public static async Task BuildCatalogAsync(string path, Storage storage, IEnumerable<string> ids)
        {
            AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage, 600);

            const int BatchSize = 200;
            int i = 0;

            int commitCount = 0;

            IEnumerable<string> files = GetFileList(path, ids);

            Console.WriteLine("initial build files = {0}", files.Count());

            foreach (string fullName in files)
            {
                writer.Add(new NuspecPackageCatalogItem(fullName));

                if (++i % BatchSize == 0)
                {
                    await writer.Commit(DateTime.UtcNow);

                    Console.WriteLine("commit number {0}", commitCount++);
                }
            }

            await writer.Commit(DateTime.UtcNow);

            Console.WriteLine("commit number {0}", commitCount++);
        }

        public static async Task AddDependenciesAsync(string path, Storage storage)
        {
            Uri root = storage.ResolveUri("index.json");

            Console.WriteLine(root);

            DistinctPackageIdCollector distinctPackageIdCollector = new DistinctPackageIdCollector(root);
            await distinctPackageIdCollector.Run();

            DistinctDependencyPackageIdCollector distinctDependencyPackageIdCollector = new DistinctDependencyPackageIdCollector(root);
            await distinctDependencyPackageIdCollector.Run();

            HashSet<string> missing = new HashSet<string>();

            foreach (string id in distinctDependencyPackageIdCollector.Result)
            {
                if (!distinctPackageIdCollector.Result.Contains(id))
                {
                    if (!id.StartsWith("../"))
                    {
                        missing.Add(id);
                    }
                }
            }

            if (missing.Count > 0)
            {
                Console.WriteLine("missing: {0}", missing.Count);

                foreach (string name in missing)
                {
                    Console.WriteLine("\t{0}", name);
                }

                BuildCatalogAsync(path, storage, missing).Wait();
                AddDependenciesAsync(path, storage).Wait();
            }
        }

        public static void Test0()
        {
            Console.WriteLine("MakeTestCatalog.Test0");

            //Storage storage = new FileStorage("http://localhost:8000/test", @"c:\data\site\test");

            StorageCredentials credentials = new StorageCredentials("", "");
            CloudStorageAccount account = new CloudStorageAccount(credentials, true);
            Storage storage = new AzureStorage(account, "ver36", "catalog");

            var ids = GetInitialIdList(250);

            //var ids = new string[] { "dotnetrdf" };

            string path = @"c:\data\nuget\nuspecs";

            BuildCatalogAsync(path, storage, ids).Wait();
            AddDependenciesAsync(path, storage).Wait();

            DistinctPackageIdCollector distinctPackageIdCollector = new DistinctPackageIdCollector(storage.ResolveUri("index.json"));
            distinctPackageIdCollector.Run().Wait();

            Console.WriteLine(distinctPackageIdCollector.Result.Count);
        }
    }
}
