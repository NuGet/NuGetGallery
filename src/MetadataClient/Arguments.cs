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
using NuGet.Services.Metadata.Catalog.Persistence;
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

        private double GetMemoryInMB()
        {
            return (double)GC.GetTotalMemory(forceFullCollection: true) / (1024 * 1024);
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
