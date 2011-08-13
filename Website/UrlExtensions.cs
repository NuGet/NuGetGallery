using System.Web.Mvc;

namespace NuGetGallery {
    public static class UrlExtensions {
        public static string Home(this UrlHelper url) {
            return url.RouteUrl(RouteName.Home);
        }

        public static string Account(this UrlHelper url) {
            return url.RouteUrl(RouteName.Account);
        }

        public static string Register(this UrlHelper url) {
            return url.RouteUrl(RouteName.Register);
        }

        public static string PackageList(this UrlHelper url) {
            return url.RouteUrl(RouteName.ListPackages);
        }

        public static string Package(this UrlHelper url, string id) {
            return url.Package(id, null);
        }

        public static string Package(this UrlHelper url, string id, string version) {
            return url.RouteUrl(RouteName.DisplayPackage, new { id, version });
        }

        public static string Package(this UrlHelper url, Package package) {
            return url.Package(package.PackageRegistration.Id, package.Version);
        }

        public static string Package(this UrlHelper url, DisplayPackageViewModel package) {
            return url.Package(package.Id, package.Version);
        }

        public static string Package(this UrlHelper url, PackageRegistration package) {
            return url.Package(package.Id);
        }

        public static string Package(this UrlHelper url, DisplayPackageViewModel package, PackageAction action) {
            return url.RouteUrl(RouteName.PackageAction, new { id = package.Id, action = action.ToString() });
        }

        public static string Package(this UrlHelper url, DisplayPackageViewModel package, PackageVersionAction action) {
            return url.RouteUrl(RouteName.PackageVersionAction, new { id = package.Id, version = package.Version, action = action.ToString() });
        }

        public static string Package(this UrlHelper url, Package package, PackageVersionAction action) {
            return url.RouteUrl(RouteName.PackageVersionAction, new { id = package.PackageRegistration.Id, version = package.Version, action = action.ToString() });
        }

        public static string PackageDownload(this UrlHelper url, string id, string version) {
            return url.RouteUrl(RouteName.PackageVersionAction,
                new { id, version, action = ActionName.DownloadPackage },
                "http");
        }

        public static string LogOn(this UrlHelper url) {
            return url.RouteUrl(RouteName.Authentication, new { action = ActionName.LogOn, returnUrl = url.RequestContext.HttpContext.Request.Url });
        }

        public static string LogOff(this UrlHelper url) {
            return url.RouteUrl(RouteName.Authentication, new { action = ActionName.LogOff, ReturnUrl = url.RequestContext.HttpContext.Request.Url });
        }

        public static string Search(this UrlHelper url, string searchTerm) {
            return url.RouteUrl(RouteName.ListPackages, new { q = searchTerm });
        }

        public static string UploadPackage(this UrlHelper url) {
            return url.RouteUrl(RouteName.UploadPackage);
        }
    }

    public enum PackageVersionAction {
        ReportAbuse,
        EditPackage,
        PublishPackage
    }

    public enum PackageAction {
        ContactOwners,
        ManagePackageOwners
    }
}