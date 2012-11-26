using System.IO;
using System.Web;
using Lucene.Net.Util;
using System.Web.Hosting;

namespace NuGetGallery
{
    internal static class LuceneCommon
    {
        internal static readonly string IndexDirectory;
        internal static readonly string IndexMetadataPath;
        internal static readonly Version LuceneVersion = Version.LUCENE_29;

        static LuceneCommon()
        {
            // Fall back to a temp folder when run with no HostingEnvironment i.e. in context of unit tests.
            IndexDirectory = HostingEnvironment.MapPath("~/AppData/Lucene") ??
                Path.Combine(Path.GetTempPath(), "AppData", "Lucene");
            IndexMetadataPath = Path.Combine(IndexDirectory, "index.metadata");
        }
    }
}