using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Packaging;
using NuGet.PackagingCore;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SimpleGalleryLib
{
    public static class SimpleGalleryAPI
    {
        private static string _connectionString;
        public static string ConnectionString
        {
            get
            {
                if (_connectionString == null)
                {
                    // load from the environment variable for web jobs
                    _connectionString = Environment.GetEnvironmentVariable("StorageConnectionString");
                }

                return _connectionString;
            }

            set
            {
                _connectionString = value;
            }
        }

        public static void DeleteCatalogItems()
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(ConnectionString);
            var client = account.CreateCloudBlobClient();

            var catalogContainer = client.GetContainerReference("catalog-0");

            foreach (var blob in catalogContainer.ListBlobs(string.Empty, true))
            {
                var blobItem = client.GetBlobReferenceFromServer(blob.Uri);
                blobItem.DeleteIfExists();
            }
        }

        public static string[] GetCatalogItems()
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(ConnectionString);
            var client = account.CreateCloudBlobClient();

            var catalogContainer = client.GetContainerReference("catalog-0");

            List<string> items = new List<string>();

            foreach (var blob in catalogContainer.ListBlobs(string.Empty, true))
            {
                items.Add(blob.Uri.AbsoluteUri);
            }

            return items.ToArray();
        }

        public static CatalogItem CreateCatalogItem(Stream stream, DateTime? published, string packageHash, string originName, Uri downloadUrl)
        {
            try
            {
                Tuple<XDocument, IEnumerable<PackageEntry>, long, string> metadata = GetNupkgMetadata(stream, packageHash);

                // additional sections
                var addons = new GraphAddon[] { new MetadataAddon(stream), new DownloadUrlAddon(downloadUrl) };

                return new NuspecPackageCatalogItem(metadata.Item1, published, metadata.Item2, metadata.Item3, metadata.Item4, addons);
            }
            catch (InvalidDataException e)
            {
                Trace.TraceError("Exception: {0} {1} {2}", originName, e.GetType().Name, e.Message);
                return null;
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("Exception processing {0}", originName), e);
            }
        }

        public static Tuple<XDocument, IEnumerable<PackageEntry>, long, string> GetNupkgMetadata(Stream stream, string hash = null)
        {
            long packageFileSize = stream.Length;

            string packageHash = hash;

            if (String.IsNullOrEmpty(packageHash))
            {
                packageHash = Utils.GenerateHash(stream);
            }

            stream.Seek(0, SeekOrigin.Begin);

            using (ZipArchive package = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                XDocument nuspec = Utils.GetNuspec(package);

                if (nuspec == null)
                {
                    throw new InvalidDataException("Unable to find nuspec");
                }

                IEnumerable<PackageEntry> entries = GetEntries(package);

                return Tuple.Create(nuspec, entries, packageFileSize, packageHash);
            }
        }

        public static IEnumerable<PackageEntry> GetEntries(ZipArchive package)
        {
            IList<PackageEntry> result = new List<PackageEntry>();

            foreach (ZipArchiveEntry entry in package.Entries)
            {
                if (entry.FullName.EndsWith("/.rels", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (entry.FullName.EndsWith("[Content_Types].xml", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (entry.FullName.EndsWith(".psmdcp", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(new PackageEntry(entry));
            }

            return result;
        }

        public static CatalogItem GetCatalogItem(Stream nupkgStream, DateTime published, Uri downloadUrl)
        {
            nupkgStream.Seek(0, SeekOrigin.Begin);

            string hash = Utils.GenerateHash(nupkgStream);

            nupkgStream.Seek(0, SeekOrigin.Begin);
            CatalogItem item = CreateCatalogItem(nupkgStream, published, hash, "form upload", downloadUrl);

            return item;
        }

        /// <summary>
        /// Adds a nupkg to a catalog and registration blob set.
        /// </summary>
        /// <param name="account">Azure storage account containing the catalog.</param>
        /// <param name="nupkgStream">package</param>
        public static void AddPackage(CloudStorageAccount account, Stream nupkgStream)
        {
            var client = account.CreateCloudBlobClient();
            var packages = client.GetContainerReference("packages");
            Uri packagesUri = packages.Uri;

            packages.CreateIfNotExists();

            var identity = GetPackageIdAndVersion(nupkgStream);

            string nupkgName = String.Format(CultureInfo.InvariantCulture, "{0}.{1}.nupkg", identity.Id, identity.Version.ToNormalizedString()).ToLowerInvariant();

            var nupkgBlob = packages.GetBlockBlobReference(nupkgName);

            // TODO: add handling for existing packages
            //if (!nupkgBlob.Exists()) 

            DateTime now = DateTime.UtcNow;

            CatalogItem item = GetCatalogItem(nupkgStream, now, nupkgBlob.Uri);

            AzureStorage storage = new AzureStorage(account, "catalog-0");
            AzureStorageFactory storageFactory = new AzureStorageFactory(account, "registrations-0", null, client.BaseUri);

            AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage, 550, true);

            writer.Add(item);

            Uri catalogIndex = new Uri(account.BlobStorageUri.PrimaryUri, "catalog-0/index.json");

            var metadata = PackageCatalog.CreateCommitMetadata(catalogIndex, now, now);

            writer.Commit(now, metadata).Wait();

            RegistrationCatalogCollector regCollector = new RegistrationCatalogCollector(catalogIndex, storageFactory);
            regCollector.ContentBaseAddress = client.BaseUri;
            regCollector.Run(now.Subtract(TimeSpan.FromMilliseconds(1)), now).Wait();


            nupkgStream.Seek(0, SeekOrigin.Begin);
            nupkgBlob.UploadFromStream(nupkgStream);
        }

        public static Stream Context
        {
            get
            {
                MemoryStream stream = new MemoryStream();
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    writer.WriteLine(@" { ""@context"": {
                                                ""@vocab"": ""http://schema.nuget.org/schema#"",
                                                ""catalog"": ""http://schema.nuget.org/catalog#"",
                                                ""ema"": ""http://schema.azure.com/ema#"",
                                                ""xsd"": ""http://www.w3.org/2001/XMLSchema#"",
                                                ""dependencies"": {
                                                  ""@id"": ""dependency"",
                                                  ""@container"": ""@set""
                                                },
                                                ""dependencyGroups"": {
                                                  ""@id"": ""dependencyGroup"",
                                                  ""@container"": ""@set""
                                                },
                                                ""packageEntries"": {
                                                  ""@id"": ""packageEntry"",
                                                  ""@container"": ""@set""
                                                },
                                                ""supportedFrameworks"": {
                                                  ""@id"": ""supportedFramework"",
                                                  ""@container"": ""@set""
                                                },
                                                ""tags"": {
                                                  ""@id"": ""tag"",
                                                  ""@container"": ""@set""
                                                },
                                                ""published"": {
                                                  ""@type"": ""xsd:dateTime""
                                                },
                                                ""catalog:commitTimeStamp"": {
                                                  ""@type"": ""xsd:dateTime""
                                                }
                                              } }");

                    MemoryStream outStream = new MemoryStream();
                    stream.Seek(0, SeekOrigin.Begin);
                    stream.CopyTo(outStream);
                    return outStream;
                }
            }
        }

        public static PackageIdentity GetPackageIdAndVersion(Stream nupkg)
        {
            nupkg.Seek(0, SeekOrigin.Begin);
            ZipFileSystem zip = new ZipFileSystem(nupkg);

            using (var reader = new PackageReader(zip))
            {
                using (var nuspecStream = reader.GetNuspec())
                {
                    var nuspecReader = new NuspecReader(nuspecStream);

                    string id = nuspecReader.GetId();
                    NuGetVersion version = null;

                    NuGetVersion.TryParse(nuspecReader.GetVersion(), out version);

                    return new PackageIdentity(id, version);
                }
            }
        }

        public static bool UploadPackageToTemp(CloudStorageAccount account, FileInfo file)
        {
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference("upload-0");

            container.CreateIfNotExists();

            string name = Guid.NewGuid().ToString() + ".nupkg";
            var blob = container.GetBlockBlobReference(name);

            blob.UploadFromStream(file.OpenRead());

            return true;
        }
    }
}
