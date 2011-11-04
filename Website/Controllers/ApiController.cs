﻿using System;
using System.Web.Mvc;
using NuGet;

namespace NuGetGallery
{
    public partial class ApiController : Controller
    {
        readonly IPackageService packageSvc;
        readonly IUserService userSvc;

        public ApiController(IPackageService packageSvc, IUserService userSvc)
        {
            this.packageSvc = packageSvc;
            this.userSvc = userSvc;
        }

        [ActionName("PushPackageApi"), HttpPost, RequireRemoteHttps]
        public virtual ActionResult CreatePackage(Guid apiKey)
        {
            var user = userSvc.FindByApiKey(apiKey);
            if (user == null)
                throw new EntityException(Strings.ApiKeyNotAuthorized, "push");

            var packageToPush = ReadPackageFromRequest();

            var package = packageSvc.FindPackageByIdAndVersion(packageToPush.Id, packageToPush.Version.ToString());
            if (package != null)
                throw new EntityException(Strings.PackageExistsAndCannotBeModified, packageToPush.Id, packageToPush.Version.ToString());

            package = packageSvc.CreatePackage(packageToPush, user);
            return new EmptyResult();
        }

        [ActionName("DeletePackageApi"), HttpDelete, RequireRemoteHttps]
        public virtual ActionResult DeletePackage(Guid apiKey, string id, string version)
        {
            var user = userSvc.FindByApiKey(apiKey);
            if (user == null)
                throw new EntityException(Strings.ApiKeyNotAuthorized, "delete");

            var package = packageSvc.FindPackageByIdAndVersion(id, version);
            if (package == null)
                throw new EntityException(Strings.PackageWithIdAndVersionNotFound, id, version);

            if (!package.IsOwner(user))
            {
                throw new EntityException(Strings.ApiKeyNotAuthorized, "delete");
            }

            packageSvc.MarkPackageUnlisted(package);
            return new EmptyResult();
        }

        [ActionName("PublishPackageApi"), HttpPost, RequireRemoteHttps]
        public virtual ActionResult PublishPackage(Guid key, string id, string version)
        {
            var user = userSvc.FindByApiKey(key);
            if (user == null)
                throw new EntityException(Strings.ApiKeyNotAuthorized, "publish");

            var package = packageSvc.FindPackageByIdAndVersion(id, version);
            if (package == null)
                throw new EntityException(Strings.PackageWithIdAndVersionNotFound, id, version);

            packageSvc.PublishPackage(id, version);
            return new EmptyResult();
        }

        public virtual IPackage ReadPackageFromRequest()
        {
            return new ZipPackage(Request.InputStream);
        }
    }
}