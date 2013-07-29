using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery.Operations.Indexing
{
    public class PackageIndex : IDisposable
    {
        private AzureDirectory _directory;

        public PackageIndex(AzureDirectory directory)
        {
            _directory = directory;
        }

        public static PackageIndex Open(CloudStorageAccount account)
        {
            // Open the Azure Directory
            var directory = new AzureDirectory(account, "lucene", new RAMDirectory());

            return new PackageIndex(directory);
        }

        public void AddOrUpdate(IList<dynamic> packages)
        {
            foreach (var package in packages)
            {
                AddOrUpdate(package);
            }
        }

        public void AddOrUpdate(dynamic package)
        {
            // Build a document from a package
            var doc = PackageDocumentSerializer.ToDocument(package);
        }

        public void Dispose()
        {
            _directory.Dispose();
        }
    }
}
