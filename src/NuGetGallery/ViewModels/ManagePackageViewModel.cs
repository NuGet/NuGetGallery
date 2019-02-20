// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGet.Versioning;

namespace NuGetGallery
{
    public class ManagePackageViewModel : ListPackageItemViewModel
    {
        public ManagePackageViewModel(Package package, User currentUser, IReadOnlyList<ReportPackageReason> reasons, UrlHelper url, string readMe)
            : base(package, currentUser)
        {
            IsCurrentUserAnAdmin = currentUser != null && currentUser.IsAdministrator;
            
            DeletePackagesRequest = new DeletePackagesRequest
            {
                Packages = new List<string>
                {
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}|{1}",
                        package.PackageRegistration.Id,
                        package.Version)
                },
                ReasonChoices = reasons
            };

            IsLocked = package.PackageRegistration.IsLocked;

            var versionSelectPackages = package.PackageRegistration.Packages
                .Where(p => p.PackageStatusKey == PackageStatus.Available || p.PackageStatusKey == PackageStatus.Validating)
                .OrderByDescending(p => new NuGetVersion(p.Version))
                .ToList();

            var versionSelectListItems = new List<SelectListItem>();
            VersionListedStateDictionary = new Dictionary<string, VersionListedState>();
            VersionReadMeStateDictionary = new Dictionary<string, VersionReadMeState>();
            VersionDeprecatedStateDictionary = new Dictionary<string, VersionDeprecatedState>();

            var submitUrlTemplate = url.PackageVersionActionTemplate("Edit");
            var getReadMeUrlTemplate = url.PackageVersionActionTemplate("GetReadMeMd");
            string defaultSelectedVersion = null;
            foreach (var versionSelectPackage in versionSelectPackages)
            {
                var text = NuGetVersionFormatter.ToFullString(versionSelectPackage.Version) + (versionSelectPackage.IsLatestSemVer2 ? " (Latest)" : string.Empty);
                var value = NuGetVersionFormatter.Normalize(versionSelectPackage.Version);
                versionSelectListItems.Add(new SelectListItem
                {
                    Text = text,
                    Value = value
                });

                if (versionSelectPackage == package)
                {
                    defaultSelectedVersion = value;
                }

                VersionListedStateDictionary.Add(
                    value, 
                    new VersionListedState(versionSelectPackage));

                var model = new TrivialPackageVersionModel(versionSelectPackage);
                VersionReadMeStateDictionary.Add(
                    value, 
                    new VersionReadMeState(
                        submitUrlTemplate.Resolve(model),
                        getReadMeUrlTemplate.Resolve(model),
                        null));

                VersionDeprecatedStateDictionary.Add(
                    value,
                    new VersionDeprecatedState(versionSelectPackage));
            }

            VersionSelectList = new SelectList(
                versionSelectListItems,
                nameof(SelectListItem.Value),
                nameof(SelectListItem.Text),
                defaultSelectedVersion);

            // Update edit model with the readme.md data.
            ReadMe = new EditPackageVersionReadMeRequest();
            if (package.HasReadMe)
            {
                ReadMe.ReadMe.SourceType = ReadMeService.TypeWritten;
                ReadMe.ReadMe.SourceText = readMe;
            }
        }

        public SelectList VersionSelectList { get; set; }

        public bool IsCurrentUserAnAdmin { get; }

        public DeletePackagesRequest DeletePackagesRequest { get; set; }

        public bool IsLocked { get; }

        public EditPackageVersionReadMeRequest ReadMe { get; set; }

        public Dictionary<string, VersionListedState> VersionListedStateDictionary { get; set; }

        public Dictionary<string, VersionReadMeState> VersionReadMeStateDictionary { get; set; }
        
        public Dictionary<string, VersionDeprecatedState> VersionDeprecatedStateDictionary { get; set; }

        public class VersionListedState
        {
            public VersionListedState(Package package)
            {
                Listed = package.Listed;
                DownloadCount = package.DownloadCount;
            }

            public bool Listed { get; set; }
            public int DownloadCount { get; set; }
        }

        public class VersionReadMeState
        {
            public VersionReadMeState(string submitUrl, string getReadMeUrl, string readMe)
            {
                SubmitUrl = submitUrl;
                GetReadMeUrl = getReadMeUrl;
                ReadMe = readMe;
            }

            public string SubmitUrl { get; set; }
            public string GetReadMeUrl { get; set; }
            public string ReadMe { get; set; }
        }

        public class VersionDeprecatedState
        {
            public VersionDeprecatedState(Package package)
            {
                var deprecation = package.Deprecations.SingleOrDefault();
                if (deprecation != null)
                {
                    IsVulnerable = CheckPackageDeprecationStatusFlag(deprecation, PackageDeprecationStatus.Vulnerable);
                    IsLegacy = CheckPackageDeprecationStatusFlag(deprecation, PackageDeprecationStatus.Legacy);
                    IsOther = CheckPackageDeprecationStatusFlag(deprecation, PackageDeprecationStatus.Other);

                    CveIds = deprecation.Cves?.Select(c => c.CveId).ToList();
                    CvssRating = deprecation.CvssRating;
                    CweIds = deprecation.Cwes?.Select(c => c.CweId).ToList();

                    // A deprecation should not have both an alternate package registration and an alternate package.
                    // In case a deprecation does have both, we will hide the alternate package registration's ID in this model.
                    AlternatePackageId = deprecation.AlternatePackageRegistration?.Id;
                    var alternatePackage = deprecation.AlternatePackage;
                    AlternatePackageId = alternatePackage?.Id;
                    AlternatePackageVersion = alternatePackage?.Version;

                    CustomMessage = deprecation.CustomMessage;

                    // It doesn't make sense to unlist packages that are already unlisted.
                    // Additionally, if a package was not unlisted when it was deprecated, we shouldn't unlist it when its deprecation information is updated.
                    ShouldUnlist = package.Listed && deprecation.Status == PackageDeprecationStatus.NotDeprecated;
                }
            }

            public bool IsVulnerable { get; set; }
            public bool IsLegacy { get; set; }
            public bool IsOther { get; set; }
            public IReadOnlyCollection<string> CveIds { get; set; }
            public decimal? CvssRating { get; set; }
            public IReadOnlyCollection<string> CweIds { get; set; }
            public string AlternatePackageId { get; set; }
            public string AlternatePackageVersion { get; set; }
            public string CustomMessage { get; set; }
            public bool ShouldUnlist { get; set; }

            private bool CheckPackageDeprecationStatusFlag(PackageDeprecation deprecation, PackageDeprecationStatus flag)
            {
                return (deprecation.Status & flag) > PackageDeprecationStatus.NotDeprecated;
            }
        }
    }
}