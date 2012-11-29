using System.IO;
using System.Web;
using Lucene.Net.Util;
using System.Web.Hosting;
using Lucene.Net.Store;

namespace NuGetGallery
{
    internal static class LuceneCommon
    {
        internal static readonly string IndexDirectory;
        internal static readonly string IndexMetadataPath;
        internal static readonly Version LuceneVersion = Version.LUCENE_29;

        static LuceneCommon()
        {
            IndexDirectory = HostingEnvironment.MapPath("~/App_Data/Lucene");
            IndexMetadataPath = Path.Combine(IndexDirectory ?? ".", "index.metadata");
        }

        // Factory method for DI/IOC that creates the directory the index is stored in.
        // Used by real website. Bypassed for unit tests.
        internal static Lucene.Net.Store.Directory GetDirectory()
        {
            if (!System.IO.Directory.Exists(IndexDirectory))
            {
                System.IO.Directory.CreateDirectory(IndexDirectory);
            }

            var directoryInfo = new DirectoryInfo(IndexDirectory);
            return new SimpleFSDirectory(directoryInfo);
        }

        public static Lucene.Net.Store.Directory GetRAMDirectory()
        {
            return new RAMDirectory();
        }
    }
}