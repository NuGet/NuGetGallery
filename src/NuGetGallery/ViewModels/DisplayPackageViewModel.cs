// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet;
using NuGet.Services.Gallery;
using NuGet.Services.Gallery.Entities;
using NuGet.Versioning;

namespace NuGetGallery
{
    public class DisplayPackageViewModel : ListPackageItemViewModel
    {
        public DisplayPackageViewModel(Package package)
            : this(package, false)
        {
        }

        public DisplayPackageViewModel(Package package, bool isVersionHistory)
            : base(package)
        {
            Copyright = package.Copyright;
            if (!isVersionHistory)
            {
                Dependencies = new DependencySetsViewModel(package.Dependencies);
                PackageVersions = from p in package.PackageRegistration.Packages.ToList()
                                  orderby new NuGetVersion(p.Version) descending
                                  select new DisplayPackageViewModel(p, isVersionHistory: true);
            }
            DownloadCount = package.DownloadCount;
            LastEdited = package.LastEdited;

            // calculate the number of days since the package was created
            // round to the nearest integer, with a min value of 1
            // divide the total download count by this number
            TotalDaysSinceCreated = Convert.ToInt32(Math.Max(1, Math.Round((DateTime.UtcNow - package.Created).TotalDays)));
            DownloadsPerDay = TotalDownloadCount / TotalDaysSinceCreated;
        }

        public void SetPendingMetadata(PackageEdit pendingMetadata)
        {
            if (pendingMetadata.TriedCount < 3)
            {
                HasPendingMetadata = true;
                Authors = pendingMetadata.Authors;
                Copyright = pendingMetadata.Copyright;
                Description = pendingMetadata.Description;
                IconUrl = pendingMetadata.IconUrl;
                LicenseUrl = pendingMetadata.LicenseUrl;
                ProjectUrl = pendingMetadata.ProjectUrl;
                ReleaseNotes = pendingMetadata.ReleaseNotes;
                Tags = pendingMetadata.Tags.ToStringSafe().Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                Title = pendingMetadata.Title;
            }
            else
            {
                HasPendingMetadata = false;
                IsLastEditFailed = true;
            }
        }

        public DependencySetsViewModel Dependencies { get; set; }
        public IEnumerable<DisplayPackageViewModel> PackageVersions { get; set; }
        public string Copyright { get; set; }

        public bool HasPendingMetadata { get; private set; }
        public bool IsLastEditFailed { get; private set; }
        public DateTime? LastEdited { get; set; }
        public int DownloadsPerDay { get; private set; }
        public int TotalDaysSinceCreated { get; private set; }

        public bool HasNewerPrerelease
        {
            get
            {
                return PackageVersions.Any(pv => pv.LatestVersion && !pv.LatestStableVersion);
            }
        }

        public bool? IsIndexed { get; set; }
    }
}