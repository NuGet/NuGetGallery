﻿// Copyright (c) .NET Foundation. All rights reserved.
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
        public ManagePackageViewModel(Package package, User currentUser, IReadOnlyList<ReportPackageReason> reasons, UrlHelper url, string readMe, bool isManageDeprecationEnabled)
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

            IsManageDeprecationEnabled = isManageDeprecationEnabled;

            var versionSelectPackages = package.PackageRegistration.Packages
                .Where(p => p.PackageStatusKey == PackageStatus.Available || p.PackageStatusKey == PackageStatus.Validating)
                .OrderByDescending(p => new NuGetVersion(p.Version))
                .ToList();

            VersionSelectList = new List<SelectListItem>();
            VersionListedStateDictionary = new Dictionary<string, VersionListedState>();
            VersionReadMeStateDictionary = new Dictionary<string, VersionReadMeState>();
            VersionDeprecationStateDictionary = new Dictionary<string, VersionDeprecationState>();

            var submitUrlTemplate = url.PackageVersionActionTemplate("Edit");
            var getReadMeUrlTemplate = url.PackageVersionActionTemplate("GetReadMeMd");
            foreach (var versionSelectPackage in versionSelectPackages)
            {
                var text = PackageHelper.GetSelectListText(versionSelectPackage);
                var value = NuGetVersionFormatter.Normalize(versionSelectPackage.Version);
                VersionSelectList.Add(new SelectListItem
                {
                    Text = text,
                    Value = value,
                    Selected = package == versionSelectPackage
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

                VersionDeprecationStateDictionary.Add(
                    value,
                    new VersionDeprecationState(versionSelectPackage, text));
            }

            // Update edit model with the readme.md data.
            ReadMe = new EditPackageVersionReadMeRequest();
            if (package.HasReadMe)
            {
                ReadMe.ReadMe.SourceType = ReadMeService.TypeWritten;
                ReadMe.ReadMe.SourceText = readMe;
            }
        }

        public List<SelectListItem> VersionSelectList { get; set; }

        public bool IsCurrentUserAnAdmin { get; }

        public DeletePackagesRequest DeletePackagesRequest { get; set; }

        public bool IsLocked { get; }

        public EditPackageVersionReadMeRequest ReadMe { get; set; }

        public Dictionary<string, VersionListedState> VersionListedStateDictionary { get; set; }

        public Dictionary<string, VersionReadMeState> VersionReadMeStateDictionary { get; set; }

        public bool IsManageDeprecationEnabled { get; }
        
        public Dictionary<string, VersionDeprecationState> VersionDeprecationStateDictionary { get; set; }

        /// <remarks>
        /// The schema of this class is shared with the client-side Javascript to share information about package listing state.
        /// The JS expects the exact naming of its properties. Do not change the naming without updating the JS.
        /// </remarks>
        public class VersionListedState
        {
            public VersionListedState(Package package)
            {
                Listed = package.Listed;
                DownloadCount = package.DownloadCount;
            }

            public bool Listed { get; }
            public int DownloadCount { get; }
        }

        /// <remarks>
        /// The schema of this class is shared with the client-side Javascript to share information about package ReadMe state.
        /// The JS expects the exact naming of its properties. Do not change the naming without updating the JS.
        /// </remarks>
        public class VersionReadMeState
        {
            public VersionReadMeState(string submitUrl, string getReadMeUrl, string readMe)
            {
                SubmitUrl = submitUrl;
                GetReadMeUrl = getReadMeUrl;
                ReadMe = readMe;
            }

            public string SubmitUrl { get; }
            public string GetReadMeUrl { get; }
            public string ReadMe { get; }
        }

        /// <remarks>
        /// The schema of this class is shared with the client-side Javascript to share information about package deprecation state.
        /// The JS expects the exact naming of its properties. Do not change the naming without updating the JS.
        /// </remarks>
        public class VersionDeprecationState
        {
            public VersionDeprecationState(Package package, string text)
            {
                Text = text;

                var deprecation = package.Deprecations.SingleOrDefault();
                if (deprecation != null)
                {
                    IsVulnerable = deprecation.Status.HasFlag(PackageDeprecationStatus.Vulnerable);
                    IsLegacy = deprecation.Status.HasFlag(PackageDeprecationStatus.Legacy);
                    IsOther = deprecation.Status.HasFlag(PackageDeprecationStatus.Other);

                    CveIds = deprecation.Cves?.Select(c => new VulnerabilityDetailState(c)).ToList();
                    CvssRating = deprecation.CvssRating;
                    CweIds = deprecation.Cwes?.Select(c => new VulnerabilityDetailState(c)).ToList();

                    AlternatePackageId = deprecation.AlternatePackageRegistration?.Id;

                    var alternatePackage = deprecation.AlternatePackage;
                    if (alternatePackage != null)
                    {
                        // A deprecation should not have both an alternate package registration and an alternate package.
                        // In case a deprecation does have both, we will hide the alternate package registration's ID in this model.
                        AlternatePackageId = alternatePackage?.Id;
                        AlternatePackageVersion = alternatePackage?.Version;
                    }

                    CustomMessage = deprecation.CustomMessage;
                }
            }

            public string Text { get; }
            public bool IsVulnerable { get; }
            public bool IsLegacy { get; }
            public bool IsOther { get; }
            public IReadOnlyCollection<VulnerabilityDetailState> CveIds { get; }
            public decimal? CvssRating { get; }
            public IReadOnlyCollection<VulnerabilityDetailState> CweIds { get; }
            public string AlternatePackageId { get; }
            public string AlternatePackageVersion { get; }
            public string CustomMessage { get; }

            /// <remarks>
            /// Ideally, the fields of this class would be pascal-case.
            /// Unfortunately, however, the serializer that MVC uses interally (JavaScriptSerializer) does not support changing the serialized name of a property.
            /// The JS on the client-side depends on the exact naming of these fields, and it would add unnecessary complexity to do the conversion on the client-side.
            /// </remarks>
            public class VulnerabilityDetailState
            {
                public VulnerabilityDetailState(Cve cve)
                    : this(cve.CveId, cve.Description)
                {
                    cvss = cve.CvssRating;
                }

                public VulnerabilityDetailState(Cwe cwe)
                    : this (cwe.CweId, cwe.Description)
                {
                    name = cwe.Name;
                }

                private VulnerabilityDetailState(string id, string description)
                {
                    this.id = id;
                    this.description = description;
                }

                public string id { get; }
                public string name { get; }
                public string description { get; }
                public decimal? cvss { get; }
                public bool fromAutocomplete => true;
            }
        }
    }
}