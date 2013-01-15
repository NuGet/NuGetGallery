using System;
using System.Linq;

namespace NuGetGallery
{
    public interface IPackageSource
    {
        IQueryable<Package> GetPackagesForIndexing(DateTime? newerThan);
    }
}