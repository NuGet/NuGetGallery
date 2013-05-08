using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.Statistics
{
    public static class ReportNames
    {
        public static readonly string RecentPackageDownloads = "recentpopularity";
        public static readonly string RecentPackageVersionDownloads = "recentpopularitydetail";
        
        public static string DownloadsForPackage(string id)
        {
            return "RecentPopularityDetail_" + id;
        }
    }
}