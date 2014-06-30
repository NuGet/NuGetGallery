using System;
using System.Linq;
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
using Newtonsoft.Json.Linq;

namespace MetadataClient
{
    public class Arguments
    {
        [ArgActionMethod]
        public void ReadChecksum(ReadChecksumArgs args)
        {
            // Load the checksum file
            var file = JObject.Parse(File.ReadAllText(args.ChecksumFile));
            var data = file.Value<JObject>("data");
            
            // Fetch all records matching the key
            foreach (var record in data.Properties().Where(p => Int32.Parse(p.Name) == args.PackageKey))
            {
                Console.WriteLine("Checksum record: " + record.ToString());
                Console.WriteLine("Checksum Value: {0}", record.ToObject<string>());
            }
        }

        [ArgActionMethod]
        public void UpdateCatalog(UpdateCatalogArgs args)
        {
            if (!args.BaseAddress.ToString().EndsWith("/"))
            {
                args.BaseAddress = new Uri(args.BaseAddress.ToString() + "/");
            }

            // Create HttpClient
            var handler = new FileSystemEmulatorHandler(
                new WebRequestHandler { AllowPipelining = true })
            {
                RootFolder = args.CatalogFolder,
                BaseAddress = args.BaseAddress
            };

            var writer = new CatalogWriter(
                new FileStorage(args.BaseAddress, args.CatalogFolder),
                new CatalogContext());
            var batcher = new GalleryExportBatcher(2000, writer);

            using (var client = new CollectorHttpClient(handler))
            {
                var startMB = GetMemoryInMB();
                Console.WriteLine("Memory Usage {0:0.00}MB", startMB);

                // Locate and load checksum data
                var checksumUrl = new Uri(args.BaseAddress, "checksums.v1.json");
                var checksumFile = client.GetJObjectAsync(checksumUrl).Result;
                var catalogChecksums = checksumFile
                        .Value<JObject>("data")
                        .Properties()
                        .ToDictionary(p => Int32.Parse(p.Name), p => p.Value.ToObject<string>());
                Console.WriteLine("Loaded {0} checksums from catalog...", catalogChecksums.Count);
                var catMB = GetMemoryInMB();
                Console.WriteLine("Memory Usage {0:0.00}MB, used ~{0:0.00}MB for catalog checksum storage", catMB, catMB - startMB);

                // Load checksums from database
                var databaseChecksums = new Dictionary<int, string>(catalogChecksums.Count);
                int lastKey = 0;
                while (true)
                {
                    const int BatchSize = 10000;
                    var range = GalleryExport.FetchChecksums(args.SqlConnectionString, lastKey, BatchSize).Result;
                    foreach (var pair in range)
                    {
                        databaseChecksums[pair.Key] = pair.Value;
                    }
                    if (range.Count < BatchSize)
                    {
                        break;
                    }
                    lastKey = range.Max(p => p.Key);
                    Console.WriteLine("Loaded {0} total checksums from database...", databaseChecksums.Count);
                }
                Console.WriteLine("Loaded all checksums from database.");
                var dbMB = GetMemoryInMB();

                // Diff the checksums
                var diffs = GalleryExport.CompareChecksums(catalogChecksums, databaseChecksums).ToList();

                // Print the diffs
                foreach (var diff in diffs)
                {
                    Console.WriteLine("{0} - {1}", diff.Key, diff.Result.ToString());
                }
                Console.WriteLine("Found {0} differences", diffs.Count);
                Console.WriteLine("Memory Usage {0:0.00}MB.", dbMB);
                Console.WriteLine(" Used ~{0:0.00}MB for db checksum storage", dbMB - catMB);
                Console.WriteLine(" Used ~{0:0.00}MB for total checksum storage", dbMB - startMB);

                Console.WriteLine("Adding new data to catalog");
                foreach (var diff in diffs)
                {
                    if (diff.Result == ComparisonResult.DifferentInCatalog || diff.Result == ComparisonResult.PresentInDatabaseOnly)
                    {
                        Console.WriteLine("Updating package {0} from database ...", diff.Key);
                        GalleryExport.WritePackage(args.SqlConnectionString, diff.Key, batcher).Wait();
                    }
                }
                batcher.Complete().Wait();
                writer.Commit().Wait();
            }
        }

        private double GetMemoryInMB()
        {
            return (double)GC.GetTotalMemory(forceFullCollection: true) / (1024 * 1024);
        }

        [ArgActionMethod]
        public void CollectChecksums(CollectChecksumsArgs args)
        {
            if (!args.BaseAddress.ToString().EndsWith("/"))
            {
                args.BaseAddress = new Uri(args.BaseAddress.ToString() + "/");
            }

            // Load the existing file
            var checksums = new LocalFileChecksumRecords(args.DestinationFile);
            checksums.Load().Wait();

            // Create HttpClient
            var handler = new FileSystemEmulatorHandler(
                new WebRequestHandler { AllowPipelining = true })
            {
                RootFolder = args.CatalogFolder,
                BaseAddress = args.BaseAddress
            };

            var collector = new ChecksumCollector(1000, checksums);
            collector.Trace.Listeners.Add(new ConsoleTraceListener()
            {
                Filter = new EventTypeFilter(SourceLevels.All)
            });
            collector.Trace.Switch.Level = SourceLevels.All;

            var timestamp = DateTime.UtcNow;
            Console.WriteLine("Collecting new checksums since: {0} UTC", checksums.TimestampUtc);
            using (var client = new CollectorHttpClient(handler))
            {
                collector.Run(client, args.IndexUrl, checksums.TimestampUtc).Wait();
            }
            checksums.TimestampUtc = timestamp;

            collector.Checksums.Save().Wait();
        }

        [ArgActionMethod]
        public void Rebuild(RebuildArgs args)
        {
            if (!args.BaseAddress.ToString().EndsWith("/"))
            {
                args.BaseAddress = new Uri(args.BaseAddress.ToString() + "/");
            }

            var writer = new CatalogWriter(
                new FileStorage(args.BaseAddress, args.DestinationFolder),
                new CatalogContext());
            var batcher = new GalleryExportBatcher(2000, writer);
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
