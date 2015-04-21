using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Registration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using VDS.RDF;

namespace CatalogTests
{
    public static class RegistrationMakerTests
    {
        static KeyValuePair<string, IGraph> CreateTestCatalogEntry(string id, string version, bool isDelete = false)
        {
            Uri packageContentUri = new Uri(string.Format("https://content/{0}.zip", Guid.NewGuid()));

            IGraph graph = new Graph();
            Uri subjectUri = new Uri(string.Format("https://catalog/{0}", Guid.NewGuid()));
            INode subject = graph.CreateUriNode(subjectUri);
            graph.Assert(subject, graph.CreateUriNode(Schema.Predicates.Id), graph.CreateLiteralNode(id));
            graph.Assert(subject, graph.CreateUriNode(Schema.Predicates.Version), graph.CreateLiteralNode(version));

            if (isDelete)
            {
                graph.Assert(subject, graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(Schema.DataTypes.CatalogDelete));
            }
            else
            {
                graph.Assert(subject, graph.CreateUriNode(Schema.Predicates.Published), graph.CreateLiteralNode(DateTime.UtcNow.ToString("O"), Schema.DataTypes.DateTime));
                graph.Assert(subject, graph.CreateUriNode(Schema.Predicates.PackageContent), graph.CreateUriNode(packageContentUri));
            }

            Console.WriteLine(subjectUri);

            return new KeyValuePair<string, IGraph>(subjectUri.AbsoluteUri, graph);
        }

        static IDictionary<string, IGraph> CreateTestSingleEntryBatch(string id, string version)
        {
            IDictionary<string, IGraph> batch = new Dictionary<string, IGraph>();
            batch.Add(CreateTestCatalogEntry(id, version));
            return batch;
        }

        static IDictionary<string, IGraph> CreateTestSingleEntryDeleteBatch(string id, string version)
        {
            IDictionary<string, IGraph> batch = new Dictionary<string, IGraph>();
            batch.Add(CreateTestCatalogEntry(id, version, true));
            return batch;
        }

        public static async Task Test0Async()
        {
            string path = "c:\\data\\test";

            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            if (directoryInfo.Exists)
            {
                directoryInfo.Delete(true);
            }
            directoryInfo.Create();

            IDictionary<string, IGraph> catalog = new Dictionary<string, IGraph>();
            //catalog.Add(CreateTestCatalogEntry("mypackage", "1.0.0"));
            //catalog.Add(CreateTestCatalogEntry("mypackage", "2.0.0"));
            catalog.Add(CreateTestCatalogEntry("mypackage", "3.0.0"));
            catalog.Add(CreateTestCatalogEntry("mypackage", "4.0.0"));
            catalog.Add(CreateTestCatalogEntry("mypackage", "5.0.0"));
            FileStorageFactory factory = new FileStorageFactory(new Uri("http://tempuri.org"), path);
            await RegistrationMaker.Process(new RegistrationKey("mypackage"), catalog, factory, new Uri("http://content/"), 2, 3);
        }

        public static async Task Test1Async()
        {
            string path = "c:\\data\\test";

            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            if (directoryInfo.Exists)
            {
                directoryInfo.Delete(true);
            }
            directoryInfo.Create();

            FileStorageFactory factory = new FileStorageFactory(new Uri("http://tempuri.org"), path);

            await RegistrationMaker.Process(new RegistrationKey("mypackage"), CreateTestSingleEntryBatch("mypackage", "1.0.0"), factory, new Uri("http://content/"), 2, 3);
            await RegistrationMaker.Process(new RegistrationKey("mypackage"), CreateTestSingleEntryBatch("mypackage", "2.0.0"), factory, new Uri("http://content/"), 2, 3);
            await RegistrationMaker.Process(new RegistrationKey("mypackage"), CreateTestSingleEntryBatch("mypackage", "3.0.0"), factory, new Uri("http://content/"), 2, 3);
            await RegistrationMaker.Process(new RegistrationKey("mypackage"), CreateTestSingleEntryBatch("mypackage", "4.0.0"), factory, new Uri("http://content/"), 2, 3);
            await RegistrationMaker.Process(new RegistrationKey("mypackage"), CreateTestSingleEntryBatch("mypackage", "5.0.0"), factory, new Uri("http://content/"), 2, 3);
            await RegistrationMaker.Process(new RegistrationKey("mypackage"), CreateTestSingleEntryBatch("mypackage", "6.0.0"), factory, new Uri("http://content/"), 2, 3);
        }

        public static async Task Test2Async()
        {
            string path = "c:\\data\\test";

            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            if (directoryInfo.Exists)
            {
                directoryInfo.Delete(true);
            }
            directoryInfo.Create();

            FileStorageFactory factory = new FileStorageFactory(new Uri("http://tempuri.org"), path);

            await RegistrationMaker.Process(new RegistrationKey("mypackage"), CreateTestSingleEntryBatch("mypackage", "1.0.0"), factory, new Uri("http://content/"), 10, 10);
            await RegistrationMaker.Process(new RegistrationKey("mypackage"), CreateTestSingleEntryBatch("mypackage", "2.0.0"), factory, new Uri("http://content/"), 10, 10);
            await RegistrationMaker.Process(new RegistrationKey("mypackage"), CreateTestSingleEntryBatch("mypackage", "3.0.0"), factory, new Uri("http://content/"), 10, 10);
            await RegistrationMaker.Process(new RegistrationKey("mypackage"), CreateTestSingleEntryBatch("mypackage", "4.0.0"), factory, new Uri("http://content/"), 10, 10);
            await RegistrationMaker.Process(new RegistrationKey("mypackage"), CreateTestSingleEntryBatch("mypackage", "5.0.0"), factory, new Uri("http://content/"), 10, 10);
            await RegistrationMaker.Process(new RegistrationKey("mypackage"), CreateTestSingleEntryBatch("mypackage", "6.0.0"), factory, new Uri("http://content/"), 10, 10);
        }

        public static async Task Test3Async()
        {
            string path = "c:\\data\\test";

            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            if (directoryInfo.Exists)
            {
                directoryInfo.Delete(true);
            }
            directoryInfo.Create();

            FileStorageFactory factory = new FileStorageFactory(new Uri("http://tempuri.org"), path);

            IDictionary<string, IGraph> catalog = new Dictionary<string, IGraph>();
            catalog.Add(CreateTestCatalogEntry("mypackage", "1.0.0"));
            catalog.Add(CreateTestCatalogEntry("mypackage", "2.0.0"));
            catalog.Add(CreateTestCatalogEntry("mypackage", "3.0.0"));
            await RegistrationMaker.Process(new RegistrationKey("mypackage"), catalog, factory, new Uri("http://content/"), 2, 3);

            await RegistrationMaker.Process(new RegistrationKey("mypackage"), CreateTestSingleEntryBatch("mypackage", "3.0.0"), factory, new Uri("http://content/"), 10, 10);
            await RegistrationMaker.Process(new RegistrationKey("mypackage"), CreateTestSingleEntryBatch("mypackage", "4.0.0"), factory, new Uri("http://content/"), 10, 10);
            await RegistrationMaker.Process(new RegistrationKey("mypackage"), CreateTestSingleEntryBatch("mypackage", "5.0.0"), factory, new Uri("http://content/"), 10, 10);
            await RegistrationMaker.Process(new RegistrationKey("mypackage"), CreateTestSingleEntryBatch("mypackage", "6.0.0"), factory, new Uri("http://content/"), 10, 10);

            await RegistrationMaker.Process(new RegistrationKey("mypackage"), CreateTestSingleEntryDeleteBatch("mypackage", "4.0.0"), factory, new Uri("http://content/"), 10, 10);
        }

        public static async Task Test4Async()
        {
            //Uri catalogUri = new Uri("https://nugetdevstorage.blob.core.windows.net/catalog/index.json");
            Uri catalogUri = new Uri("https://nugetjohtaylo.blob.core.windows.net/catalog/index.json");

            string path = "c:\\data\\registration20150420";

            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            if (directoryInfo.Exists)
            {
                directoryInfo.Delete(true);
            }
            directoryInfo.Create();

            FileStorageFactory factory = new FileStorageFactory(new Uri("http://tempuri.org"), path);

            CollectorBase collector = new RegistrationCollector(catalogUri, factory)
            {
                UnlistShouldDelete = true
            };

            await collector.Run();
        }
    }
}
