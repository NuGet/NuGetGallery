using System.IO;
using Lucene.Net.Util;
using System.Web.Hosting;
using Lucene.Net.Store;
using NuGetGallery.Configuration;
using Lucene.Net.Store.Azure;
using Microsoft.WindowsAzure.Storage;
using System;

namespace NuGetGallery
{
    internal static class LuceneCommon
    {
        internal static readonly Lucene.Net.Util.Version LuceneVersion = Lucene.Net.Util.Version.LUCENE_30;

        private static SingleInstanceLockFactory LuceneLock = new SingleInstanceLockFactory();

        // Factory method for DI/IOC that creates the directory the index is stored in.
        // Used by real website. Bypassed for unit tests.
        private static SimpleFSDirectory _directorySingleton;

        internal static string GetDirectoryLocation()
        {
            // Don't create the directory if it's not already present.
            return _directorySingleton == null ? null : _directorySingleton.Directory.FullName;
        }

        internal static string GetIndexMetadataPath()
        {
            // Don't create the directory if it's not already present.
            string root = _directorySingleton == null ? "." : (_directorySingleton.Directory.FullName ?? ".");
            return Path.Combine(root, "index.metadata");
        }

        internal static Lucene.Net.Store.Directory GetDirectory(LuceneIndexLocation location)
        {
            if (_directorySingleton == null)
            {
                var index = GetIndexLocation(location);
                if (!System.IO.Directory.Exists(index))
                {
                    System.IO.Directory.CreateDirectory(index);
                }

                var directoryInfo = new DirectoryInfo(index);
                _directorySingleton = new SimpleFSDirectory(directoryInfo, LuceneLock);
            }

            return _directorySingleton;
        }

        internal static Lucene.Net.Store.Directory GetAzureDirectory(string storageConnectionString)
        {
            CloudStorageAccount storageAccount;
            if (CloudStorageAccount.TryParse(storageConnectionString, out storageAccount))
            {
                Lucene.Net.Store.Directory directory = new AzureDirectory(storageAccount, "lucene", new RAMDirectory());
                return directory;
            }
            else
            {
                throw new ArgumentException("argument 0 storageConnectionString failed to be parsed");
            }
        }

        private static string GetIndexLocation(LuceneIndexLocation location)
        {
            switch (location)
            {
                case LuceneIndexLocation.Temp:
                    return Path.Combine(Path.GetTempPath(), "NuGetGallery", "Lucene");
                default:
                    return HostingEnvironment.MapPath("~/App_Data/Lucene");
            }
        }
    }
}