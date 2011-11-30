using System.Web.Mvc;
using System;

namespace NuGetGallery
{
    public static class UrlExtensions
    {
        // Shorthand for current url
        public static string Current(this UrlHelper url)
        {
            return url.RequestContext.HttpContext.Request.RawUrl;
        }

        public static string Home(this UrlHelper url)
        {
            return url.RouteUrl(RouteName.Home);
        }

        public static string PackageList(this UrlHelper url, int page, string sortOrder, string searchTerm)
        {
            return url.Action(MVC.Packages.ListPackages(searchTerm, sortOrder, page));
        }

        public static string PackageList(this UrlHelper url)
        {
            return url.RouteUrl(RouteName.ListPackages);
        }

        public static string Package(this UrlHelper url, string id)
        {
            return url.Package(id, null);
        }

        public static string Package(this UrlHelper url, string id, string version)
        {
            return url.RouteUrl(RouteName.DisplayPackage, new { id, version });
        }

        public static string Package(this UrlHelper url, Package package)
        {
            return url.Package(package.PackageRegistration.Id, package.Version);
        }

        public static string Package(this UrlHelper url, IPackageVersionModel package)
        {
            return url.Package(package.Id, package.Version);
        }

        public static string Package(this UrlHelper url, PackageRegistration package)
        {
            return url.Package(package.Id);
        }

        public static string PackageDownload(this UrlHelper url, int feedVersion, string id, string version)
        {
            string routeName = "v" + feedVersion + RouteName.DownloadPackage;
            return url.RouteUrl(routeName, new { Id = id, Version = version }, protocol: "http");
        }

        public static string LogOn(this UrlHelper url)
        {
            return url.RouteUrl(RouteName.Authentication, new { action = "LogOn" });
        }

        public static string LogOff(this UrlHelper url)
        {
            return url.Action(MVC.Authentication.LogOff(url.Current()));
        }

        public static string Search(this UrlHelper url, string searchTerm)
        {
            return url.RouteUrl(RouteName.ListPackages, new { q = searchTerm });
        }

        public static string UploadPackage(this UrlHelper url)
        {
            return url.Action(MVC.Packages.UploadPackage());
        }

        public static string EditPackage(this UrlHelper url, IPackageVersionModel package)
        {
            return url.Action(MVC.Packages.Edit(package.Id, package.Version));
        }

        public static string DeletePackage(this UrlHelper url, IPackageVersionModel package)
        {
            return url.Action(MVC.Packages.Delete(package.Id, package.Version));
        }

        public static string ManagePackageOwners(this UrlHelper url, IPackageVersionModel package)
        {
            return url.Action(MVC.Packages.ManagePackageOwners(package.Id, package.Version));
        }

        public static string ConfirmationUrl(this UrlHelper url, ActionResult actionResult, string username, string token, string protocol)
        {
            return url.Action(actionResult.AddRouteValue("username", username).AddRouteValue("token", token), protocol: protocol);
        }

        public static string VerifyPackage(this UrlHelper url)
        {
            return url.Action(MVC.Packages.VerifyPackage());
        }
    }
}