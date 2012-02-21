using System.IO;
using System.Web;
using Lucene.Net.Util;

namespace NuGetGallery
{
    internal static class LuceneCommon
    {
        internal static readonly string IndexDirectory = Path.Combine(HttpRuntime.AppDomainAppPath, "App_Data", "Lucene");
        internal static readonly string IndexMetadataPath = Path.Combine(IndexDirectory, "index.metadata");
        internal static readonly Version LuceneVersion = Version.LUCENE_29;
    }
}