using System.Web.Mvc;

namespace NuGetGallery {
    public static class UrlExtensions {
        // Shorthand for current url
        public static string Current(this UrlHelper url) {
            return url.RequestContext.HttpContext.Request.RawUrl;
        }

        public static string Home(this UrlHelper url) {
            return url.RouteUrl(RouteName.Home);
        }

        public static string Account(this UrlHelper url) {
            return url.RouteUrl(RouteName.Account, new { action = "Account" });
        }

        public static string Account(this UrlHelper url, AccountAction action) {
            return url.RouteUrl(RouteName.Account, new { action = action.ToString() });
        }

        public static string Publish(this UrlHelper url, Package package) {
            return url.Package(package, PackageVersionAction.PublishPackage);
        }

        public static string Publish(this UrlHelper url, IPackageVersionModel package) {
            return url.Package(package, PackageVersionAction.PublishPackage);
        }

        public static string PackageList(this UrlHelper url, int page, string sortOrder, string searchTerm) {
            return url.RouteUrl(RouteName.ListPackages,
                new {
                    page,
                    q = searchTerm,
                    sortOrder,
                });
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

        public static string Package(this UrlHelper url, IPackageVersionModel package) {
            return url.Package(package.Id, package.Version);
        }

        public static string Package(this UrlHelper url, PackageRegistration package) {
            return url.Package(package.Id);
        }

        public static string Package(this UrlHelper url, string packageId, PackageAction action) {
            return url.RouteUrl(RouteName.PackageAction, new { id = packageId, action = action.ToString() });
        }

        public static string Package(this UrlHelper url, IPackageVersionModel package, PackageAction action) {
            return url.RouteUrl(RouteName.PackageAction, new { id = package.Id, action = action.ToString() });
        }

        public static string Package(this UrlHelper url, IPackageVersionModel package, PackageVersionAction action) {
            return url.RouteUrl(RouteName.PackageVersionAction, new { id = package.Id, version = package.Version, action = action.ToString() });
        }

        public static string Package(this UrlHelper url, Package package, PackageVersionAction action) {
            return url.RouteUrl(RouteName.PackageVersionAction, new { id = package.PackageRegistration.Id, version = package.Version, action = action.ToString() });
        }

        public static string PackageDownload(this UrlHelper url, string id, string version) {
            return url.RouteUrl(RouteName.PackageVersionAction,
                new { id, version, action = "DownloadPackage" },
                "http");
        }

        public static string LogOn(this UrlHelper url) {
            return url.RouteUrl(RouteName.Authentication, new { action = "LogOn", returnUrl = url.Current() });
        }

        public static string LogOff(this UrlHelper url) {
            return url.RouteUrl(RouteName.Authentication, new { action = "LogOff", ReturnUrl = url.Current() });
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

    public enum AccountAction {
        Register,
        ChangePassword,
        Packages,
        GenerateApiKey,
        ForgotPassword
    }
}