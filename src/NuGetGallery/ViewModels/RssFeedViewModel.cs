using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class RssFeedViewModel
    {
        public string PackageId { get; internal set; }
        public string PackageDescription { get; internal set; }

        public List<Package> PackageVersions { get; internal set; }
    }
}