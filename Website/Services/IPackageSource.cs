using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.Services
{
    public interface IPackageSource
    {
        IQueryable<Package> GetPackagesForIndexing(DateTime? newerThan);
    }
}