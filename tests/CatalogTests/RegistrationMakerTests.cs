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
        static KeyValuePair<string, IGraph> CreateTestCatalogEntry(string id, string version)
        {
            Uri packageContentUri = new Uri(string.Format("https://content/{0}.zip", Guid.NewGuid()));

            IGraph graph = new Graph();
            Uri subjectUri = new Uri(string.Format("https://catalog/{0}", Guid.NewGuid()));
            INode subject = graph.CreateUriNode(subjectUri);
            graph.Assert(subject, graph.CreateUriNode(Schema.Predicates.Id), graph.CreateLiteralNode(id));
            graph.Assert(subject, graph.CreateUriNode(Schema.Predicates.Version), graph.CreateLiteralNode(version));
            graph.Assert(subject, graph.CreateUriNode(Schema.Predicates.Published), graph.CreateLiteralNode(DateTime.UtcNow.ToString("O"), Schema.DataTypes.DateTime));
            graph.Assert(subject, graph.CreateUriNode(Schema.Predicates.PackageContent), graph.CreateUriNode(packageContentUri));
            Console.WriteLine(subjectUri);

            return new KeyValuePair<string, IGraph>(subjectUri.ToString(), graph);
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
            await RegistrationMaker.Process("mypackage", catalog, factory, new Uri("http://content/"), 2, 3);
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

            IDictionary<string, IGraph> catalog1 = new Dictionary<string, IGraph>();
            catalog1.Add(CreateTestCatalogEntry("mypackage", "1.0.0"));
            await RegistrationMaker.Process("mypackage", catalog1, factory, new Uri("http://content/"), 2, 3);

            IDictionary<string, IGraph> catalog2 = new Dictionary<string, IGraph>();
            catalog2.Add(CreateTestCatalogEntry("mypackage", "2.0.0"));
            await RegistrationMaker.Process("mypackage", catalog2, factory, new Uri("http://content/"), 2, 3);

            IDictionary<string, IGraph> catalog3 = new Dictionary<string, IGraph>();
            catalog3.Add(CreateTestCatalogEntry("mypackage", "3.0.0"));
            await RegistrationMaker.Process("mypackage", catalog3, factory, new Uri("http://content/"), 2, 3);

            IDictionary<string, IGraph> catalog4 = new Dictionary<string, IGraph>();
            catalog4.Add(CreateTestCatalogEntry("mypackage", "4.0.0"));
            await RegistrationMaker.Process("mypackage", catalog4, factory, new Uri("http://content/"), 2, 3);

            IDictionary<string, IGraph> catalog5 = new Dictionary<string, IGraph>();
            catalog5.Add(CreateTestCatalogEntry("mypackage", "5.0.0"));
            await RegistrationMaker.Process("mypackage", catalog5, factory, new Uri("http://content/"), 2, 3);

            IDictionary<string, IGraph> catalog6 = new Dictionary<string, IGraph>();
            catalog6.Add(CreateTestCatalogEntry("mypackage", "6.0.0"));
            await RegistrationMaker.Process("mypackage", catalog6, factory, new Uri("http://content/"), 2, 3);
        }
    }
}
