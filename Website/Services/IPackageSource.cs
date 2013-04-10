using System;
using System.Linq;

namespace NuGetGallery
{
    public interface IPackageSource
    {
        IQueryable<PackageIndexEntity> GetPackagesForIndexing(DateTime? newerThan);
    }
}