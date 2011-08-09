using System;
using System.Web;
using System.Web.Hosting;

namespace NuGet.Server.Infrastructure {
    public class PackageUtility {
        internal static string PackagePhysicalPath = HostingEnvironment.MapPath("~/Packages");

        public static Uri GetPackageUrl(string path, Uri baseUri) {
            return new Uri(baseUri, GetPackageDownloadUrl(path));
        }

        private static string GetPackageDownloadUrl(string path) {
            return VirtualPathUtility.ToAbsolute("~/Packages/" + path);
        }
    }
}
