using System.IO;
using Lucene.Net.Util;
using System.Web.Hosting;
using Lucene.Net.Store;

namespace NuGetGallery
{
    internal static class LuceneCommon
    {
        internal static readonly string IndexDirectory = HostingEnvironment.MapPath("~/App_Data/Lucene");
        internal static readonly string IndexMetadataPath = Path.Combine(IndexDirectory ?? ".", "index.metadata");
        internal static readonly Version LuceneVersion = Version.LUCENE_30;

        // Factory method for DI/IOC that creates the directory the index is stored in.
        // Used by real website. Bypassed for unit tests.
        private static SimpleFSDirectory _directorySingleton;

        internal static Lucene.Net.Store.Directory GetDirectory()
        {
            if (_directorySingleton == null)
            {
                if (!System.IO.Directory.Exists(IndexDirectory))
                {
                    System.IO.Directory.CreateDirectory(IndexDirectory);
                }

                var directoryInfo = new DirectoryInfo(IndexDirectory);
                _directorySingleton = new SimpleFSDirectory(directoryInfo);
            }

            return _directorySingleton;
        }
    }
}