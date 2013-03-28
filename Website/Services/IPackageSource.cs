using System;
using System.Linq;
using NuGetGallery.Data.Model;

namespace NuGetGallery
{
    public interface IPackageSource
    {
        IQueryable<Package> GetPackagesForIndexing(DateTime? newerThan);
    }
}