using System;
using System.IO;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using PowerArgs;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.IO.Packaging;
using System.Net.Mime;
using System.Diagnostics;
using System.Data.SqlClient;
using NuGet.Services.Metadata.Catalog.GalleryIntegration;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Collecting;
using NuGet.Services.Metadata.Catalog;
using System.Net.Http;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace MetadataClient
{
    public class Arguments
    {
        [ArgActionMethod]
        public void UpdateCatalog(UpdateCatalogArgs args)
        {
            // Create HttpClient
            var handler = new FileSystemEmulatorHandler(
                new WebRequestHandler { AllowPipelining = true })
            {
                RootFolder = args.CatalogRootUrl,
                BaseAddress = args.BaseAddress
            };

            using (var client = new CollectorHttpClient(handler))

                // Load checksum data
                var checksums = client.GetJObjectAsync(
                    new Uri(args.BaseAddress
                    }
        }

        [ArgActionMethod]
        public void CollectChecksums(CollectChecksumsArgs args)
        {
            // Create HttpClient
            var handler = new FileSystemEmulatorHandler(
                new WebRequestHandler { AllowPipelining = true })
            {
                RootFolder = args.CatalogFolder,
                BaseAddress = args.BaseAddress
            };

            var collector = new ChecksumCollector(1000);
            collector.Trace.Listeners.Add(new ConsoleTraceListener()
            {
                Filter = new EventTypeFilter(SourceLevels.All)
            });
            collector.Trace.Switch.Level = SourceLevels.All;
            using (var client = new CollectorHttpClient(handler))
            {
                collector.Run(client, args.IndexUrl, DateTime.MinValue).Wait();
            }

            var obj = collector.Complete();

            Console.WriteLine("Writing output file...");
            using (var stream = new FileStream(args.DestinationFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            using (var sw = new StreamWriter(stream))
            using (var writer = new JsonTextWriter(sw))
            {
                obj.WriteTo(writer);
            }
        }

        [ArgActionMethod]
        public void Rebuild(RebuildArgs args)
        {
            var writer = new CatalogWriter(
                new FileStorage(args.BaseAddress, args.DestinationFolder),
                new CatalogContext());
            var batcher = new GalleryExportBatcher(1000, writer);
            int lastHighest = 0;
            while(true) {
                var range = GalleryExport.GetNextRange(
                    args.SqlConnectionString,
                    lastHighest,
                    2000).Result;
                if (range.Item1 == 0 && range.Item2 == 0)
                {
                    break;
                }
                Console.WriteLine("Writing packages with Keys {0}-{1} to catalog...", range.Item1, range.Item2);
                GalleryExport.WriteRange(
                    args.SqlConnectionString,
                    range,
                    batcher).Wait();
                lastHighest = range.Item2;
            }
            batcher.Complete().Wait();
        }

        [ArgActionMethod]
        public void UploadPackage(UploadPackageArgs args)
        {
            if (String.IsNullOrEmpty(args.ContainerName))
            {
                args.ContainerName = "received";
            }

            if (String.IsNullOrEmpty(args.CacheControl))
            {
                args.CacheControl = "no-cache";
            }

            if (String.IsNullOrEmpty(args.ContentType))
            {
                args.ContentType = "application/octet-stream";
            }

            CloudStorageAccount account = CloudStorageAccount.Parse(args.StorageConnectionString);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(args.ContainerName);

            if(container.CreateIfNotExists())
            {
                Console.WriteLine("Created '{0}' received container", args.ContainerName);
            }

            string filename = args.Path.Substring(args.Path.LastIndexOf('\\') + 1);

            CloudBlockBlob blob = container.GetBlockBlobReference(filename);

            blob.Properties.CacheControl = args.CacheControl;

            blob.Properties.ContentType = args.ContentType;

            blob.UploadFromFile(args.Path, FileMode.Open);
        }

        [ArgActionMethod]
        public void RenameOwner(RenameOwnerArgs args)
        {
            throw new NotImplementedException();
        }

        [ArgActionMethod]
        public void Pack(PackArgs args)
        {
            string filename = args.Nuspec;

            FileStream stream = new FileStream(filename, FileMode.Open);
            XDocument nuspec = XDocument.Load(stream);

            IXmlNamespaceResolver resolver = CreateNamespaceResolver();

            string id = nuspec.XPathSelectElement("nuget:package/nuget:metadata/nuget:id", resolver).Value;
            string version = nuspec.XPathSelectElement("nuget:package/nuget:metadata/nuget:version", resolver).Value;

            Uri partUri = PackUriHelper.CreatePartUri(new Uri("/" + id + ".nuspec", UriKind.Relative));

            string packagePath = id + "." + version + ".nupkg";

            using (Package package = Package.Open(packagePath, FileMode.Create))
            {
                PackagePart packagePart = package.CreatePart(partUri, MediaTypeNames.Text.Xml);

                using (XmlWriter writer = XmlWriter.Create(packagePart.GetStream(), new XmlWriterSettings { Indent = true }))
                {
                    nuspec.WriteTo(writer);
                }
            }
        }

        [ArgActionMethod]
        public void MDTrigger(MDTriggerArgs args)
        {
            if (String.IsNullOrEmpty(args.ContainerName))
            {
                args.ContainerName = "mdtriggers";
            }

            CloudStorageAccount account = CloudStorageAccount.Parse(args.StorageConnectionString);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(args.ContainerName);
            if (container.CreateIfNotExists())
            {
                Console.WriteLine("Created '{0}' blob container", args.ContainerName);
            }
            SqlConnectionStringBuilder sql = new SqlConnectionStringBuilder(args.DBConnectionString);

            Console.WriteLine("Trimming network protocol if any");
            sql.TrimNetworkProtocol();

            MetadataTrigger.Start(account, container, sql, args.DumpToCloud).Wait();
        }

        static string Nuget = "http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd";

        static IXmlNamespaceResolver CreateNamespaceResolver()
        {
            XmlNamespaceManager namespaceManager = new XmlNamespaceManager(new NameTable());
            namespaceManager.AddNamespace("nuget", Nuget);
            return namespaceManager;
        }
    }
}
