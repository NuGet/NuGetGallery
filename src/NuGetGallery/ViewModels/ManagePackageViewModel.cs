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
            var submitUrlTemplate = url.PackageVersionActionTemplate("Edit");
            var getReadMeUrlTemplate = url.PackageVersionActionTemplate("GetReadMeMd");
            foreach (var versionSelectPackage in versionSelectPackages)
            {
                var text = NuGetVersionFormatter.Normalize(versionSelectPackage.Version) + (versionSelectPackage.IsLatestStableSemVer2 ? " (Latest)" : string.Empty);
                var value = versionSelectPackage.Version;
                versionSelectListItems.Add(new SelectListItem
                {
                    Text = text,
                    Value = value
                });

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
            }

            VersionSelectList = new SelectList(
                versionSelectListItems,
                nameof(SelectListItem.Value),
                nameof(SelectListItem.Text),
                package.Version);

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
    }
}