using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public interface ICuratedFeedService
    {
        IQueryable<Package> GetPackages(string curatedFeedName);

        IQueryable<PackageRegistration> GetPackageRegistrations(string curatedFeedName);

        int? GetKey(string curatedFeedName);
    }
}