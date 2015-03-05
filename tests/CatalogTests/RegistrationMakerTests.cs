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
    }
}
