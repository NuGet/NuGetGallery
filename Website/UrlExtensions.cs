using System.Web.Mvc;

namespace NuGetGallery {
    public static class UrlExtensions {
        public static string PackageListUrl(this UrlHelper url) {
            return url.RouteUrl(RouteName.ListPackages);
        }

        public static string PackageUrl(this UrlHelper url, string id) {
            return url.PackageUrl(id, null);
        }

        public static string PackageUrl(this UrlHelper url, string id, string version) {
            return url.RouteUrl(RouteName.DisplayPackage, new { id, version });
        }

        public static string PackageUrl(this UrlHelper url, Package package) {
            return url.PackageUrl(package.PackageRegistration.Id, package.Version);
        }

        public static string PackageUrl(this UrlHelper url, DisplayPackageViewModel package) {
            return url.PackageUrl(package.Id, package.Version);
        }

        public static string PackageUrl(this UrlHelper url, PackageRegistration package) {
            return url.PackageUrl(package.Id);
        }

        public static string PackageUrl(this UrlHelper url, DisplayPackageViewModel package, PackageAction action) {
            return url.RouteUrl(RouteName.PackageAction, new { id = package.Id, action = action.ToString() });
        }

        public static string PackageUrl(this UrlHelper url, DisplayPackageViewModel package, PackageVersionAction action) {
            return url.RouteUrl(RouteName.PackageVersionAction, new { id = package.Id, version = package.Version, action = action.ToString() });
        }

        public static string PackageUrl(this UrlHelper url, Package package, PackageVersionAction action) {
            return url.RouteUrl(RouteName.PackageVersionAction, new { id = package.PackageRegistration.Id, version = package.Version, action = action.ToString() });
        }

        public static string PackageDownloadUrl(this UrlHelper url, string id, string version) {
            return url.RouteUrl(RouteName.PackageVersionAction,
                new { id, version, action = ActionName.DownloadPackage },
                "http");
        }

        public static string LogOnUrl(this UrlHelper url) {
            return url.RouteUrl(RouteName.Authentication, new { action = ActionName.LogOn, returnUrl = url.RequestContext.HttpContext.Request.Url });
        }

        public static string LogOffUrl(this UrlHelper url) {
            return url.RouteUrl(RouteName.Authentication, new { action = ActionName.LogOff, ReturnUrl = url.RequestContext.HttpContext.Request.Url });
        }

        public static string SearchUrl(this UrlHelper url, string searchTerm) {
            return url.RouteUrl(RouteName.Search, new { q = searchTerm });
        }
    }

    public enum PackageVersionAction {
        ReportAbuse,
        EditPackage,
        PublishPackage
    }

    public enum PackageAction {
        ContactOwners,
        ManagePackageOwners,
    }
}