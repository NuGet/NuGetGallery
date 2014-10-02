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
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace MetadataClient
{
    public class Arguments
    {
        [ArgActionMethod]
        public void UploadCatalog(UploadCatalogArgs args)
        {
            var account = CloudStorageAccount.Parse(args.CatalogStorage);
            var directory = new DirectoryInfo(args.CatalogFolder);

            string containerName;
            string path;
            string[] segments = args.CatalogPath.Split('/');
            if (segments.Length > 1)
            {
                containerName = segments[0];
                path = String.Join("/", segments.Skip(1));
            }
            else
            {
                containerName = args.CatalogPath;
                path = String.Empty;
            }
            var container = account.CreateCloudBlobClient().GetContainerReference(containerName);
            var blobDir = container.GetDirectoryReference(path);

            Console.WriteLine("Uploading directory from {0} to {1}", directory.FullName, blobDir.Uri.ToString());

            int counter = 0;
            Parallel.ForEach(
                directory.GetFiles("*", SearchOption.AllDirectories),
                new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 },
                file =>
            {
                string relativePath = file.FullName.Substring(directory.FullName.Length + 1).Replace('\\', '/');
                
                var blob = blobDir.GetBlockBlobReference(relativePath);

                blob.Properties.CacheControl = "no-store";
                blob.Properties.ContentType = "application/json";
                blob.UploadFromFile(file.FullName, FileMode.Open);
                Console.WriteLine("Uploaded {0} files", Interlocked.Increment(ref counter));
            });
        }

        [ArgActionMethod]
        public void CreateResolverBlobs(CreateResolverBlobsArgs args)
        {
            var storageFactory = new FileStorageFactory(args.ResolverBase, args.ResolverFolder);
            var collector = new RegistrationCatalogCollector(storageFactory, 1000);
            var client = new CollectorHttpClient(
                new FileSystemEmulatorHandler(new WebRequestHandler() { AllowPipelining = true })
            {
                BaseAddress = args.BaseAddress,
                RootFolder = args.CatalogFolder
            });
            collector.Run(client, args.IndexUrl, DateTime.MinValue).Wait();
        }

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
            }
        }

        [ArgActionMethod]
        public void UpdateCatalog(UpdateCatalogArgs args)
        {
            if (!args.BaseAddress.ToString().EndsWith("/"))
            {
                args.BaseAddress = new Uri(args.BaseAddress.ToString() + "/");
            }
            if (args.IndexUrl == null)
            {
                args.IndexUrl = new Uri(args.BaseAddress, "index.json");
            }
            if (String.IsNullOrEmpty(args.ChecksumFile))
            {
                args.ChecksumFile = Path.Combine(args.CatalogFolder, "checksums.v1.json");
            }

            var writer = new AppendOnlyCatalogWriter(
                new FileStorage(args.BaseAddress, args.CatalogFolder));
            var client = new CollectorHttpClient(
                new FileSystemEmulatorHandler(new WebRequestHandler() { AllowPipelining = true })
            {
                BaseAddress = args.BaseAddress,
                RootFolder = args.CatalogFolder
            });
            var checksums = new LocalFileChecksumRecords(args.ChecksumFile);

            using (var updater = new CatalogUpdater(writer, checksums, client))
            {
                updater.Update(args.SqlConnectionString, args.IndexUrl).Wait();
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
            if (args.IndexUrl == null)
            {
                args.IndexUrl = new Uri(args.BaseAddress, "index.json");
            }
            if (String.IsNullOrEmpty(args.ChecksumFile))
            {
                args.ChecksumFile = Path.Combine(args.CatalogFolder, "checksums.v1.json");
            }

            // Load the existing file
            var checksums = new LocalFileChecksumRecords(args.ChecksumFile);
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
            Console.WriteLine("Collecting new checksums since: {0} UTC", checksums.Cursor);
            using (var client = new CollectorHttpClient(handler))
            {
                collector.Run(client, args.IndexUrl, checksums.Cursor).Wait();
            }
            checksums.Cursor = timestamp;

            collector.Checksums.Save().Wait();
        }

        [ArgActionMethod]
        public void Rebuild(RebuildArgs args)
        {
            if (args.BaseAddress != null && !args.BaseAddress.ToString().EndsWith("/"))
            {
                args.BaseAddress = new Uri(args.BaseAddress.ToString() + "/");
            }

            // Load storage
            Storage storage;
            if (!String.IsNullOrEmpty(args.CatalogFolder))
            {
                if (args.BaseAddress == null)
                {
                    throw new ArgumentNullException("BaseAddress");
                }
                storage = new FileStorage(args.BaseAddress, args.CatalogFolder);
            }
            else
            {
                if (String.IsNullOrEmpty(args.CatalogStorage))
                {
                    throw new ArgumentNullException("CatalogStorage");
                }
                if (String.IsNullOrEmpty(args.CatalogStoragePath))
                {
                    throw new ArgumentNullException("CatalogStoragePath");
                }
                var acct = CloudStorageAccount.Parse(args.CatalogStorage);
                var dir = GetBlobDirectory(acct, args.CatalogStoragePath);
                if (args.BaseAddress == null)
                {
                    string dirUrl = dir.Uri.ToString();
                    if (!dirUrl.EndsWith("/"))
                    {
                        dirUrl += "/";
                    }
                    args.BaseAddress = new UriBuilder(dirUrl)
                    {
                        Scheme = "http",
                        Port = 80
                    }.Uri;
                }
                storage = new AzureStorage(dir, args.BaseAddress);
            }
            Console.WriteLine("Using {0} with base address {1}", storage.GetType().Name, storage.BaseAddress);

            var writer = new AppendOnlyCatalogWriter(storage);
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

        public static CloudBlobDirectory GetBlobDirectory(CloudStorageAccount account, string path)
        {
            var client = account.CreateCloudBlobClient();

            string[] segments = path.Split('/');
            string containerName;
            string prefix;

            if (segments.Length < 2)
            {
                // No "/" segments, so the path is a container and the catalog is at the root...
                containerName = path;
                prefix = String.Empty;
            }
            else
            {
                // Found "/" segments, but we need to get the first segment to use as the container...
                containerName = segments[0];
                prefix = String.Join("/", segments.Skip(1)) + "/";
            }

            var container = client.GetContainerReference(containerName);
            var dir = container.GetDirectoryReference(prefix);
            return dir;
        }
    }
}
