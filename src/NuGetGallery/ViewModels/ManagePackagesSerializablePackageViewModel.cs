// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Helpers;

namespace NuGetGallery
{
    public class ManagePackagesSerializablePackageViewModel
    {
        public ManagePackagesSerializablePackageViewModel(
            ListPackageItemRequiredSignerViewModel package,
            RouteUrlTemplate<IPackageVersionModel> packageUrlTemplate,
            RouteUrlTemplate<IPackageVersionModel> editUrlTemplate,
            RouteUrlTemplate<IPackageVersionModel> manageOwnersUrlTemplate,
            RouteUrlTemplate<IPackageVersionModel> deleteUrlTemplate,
            RouteUrlTemplate<IPackageVersionModel> setRequiredSignerUrlTemplate,
            RouteUrlTemplate<User> profileUrlTemplate)
        {
            Id = package.Id.Abbreviate(40);
            Owners = package.Owners.Select(o => new ManagePackagesSerializableOwnerViewModel(o, profileUrlTemplate));
            TotalDownloadCount = package.TotalDownloadCount;
            LatestVersion = package.FullVersion.Abbreviate(15);
            RequiredSigner = new ManagePackagesSerializableSignerViewModel(package.RequiredSigner);
            RequiredSignerMessage = package.RequiredSignerMessage;
            AllSigners = package.AllSigners.Select(s => new ManagePackagesSerializableSignerViewModel(s));
            SetRequiredSignerUrl = setRequiredSignerUrlTemplate.Resolve(package);
            PackageIconUrl = PackageHelper.ShouldRenderUrl(package.IconUrl) ? package.IconUrl : null;
            PackageUrl = packageUrlTemplate.Resolve(package);
            EditUrl = editUrlTemplate.Resolve(package);
            ManageOwnersUrl = manageOwnersUrlTemplate.Resolve(package);
            DeleteUrl = deleteUrlTemplate.Resolve(package);
            CanEdit = package.CanEdit;
            CanManageOwners = package.CanManageOwners;
            CanDelete = package.CanUnlistOrRelist;
            CanEditRequiredSigner = package.CanEditRequiredSigner;
            ShowRequiredSigner = package.ShowRequiredSigner;
            ShowTextBox = package.ShowTextBox;
        }

        public string Id { get; }
        public IEnumerable<ManagePackagesSerializableOwnerViewModel> Owners { get; }
        public int TotalDownloadCount { get; }
        public string LatestVersion { get; }
        public ManagePackagesSerializableSignerViewModel RequiredSigner { get; }
        public string RequiredSignerMessage { get; }
        public IEnumerable<ManagePackagesSerializableSignerViewModel> AllSigners { get; }
        public string SetRequiredSignerUrl { get; }
        public string PackageIconUrl { get; }
        public string PackageUrl { get; }
        public string EditUrl { get; }
        public string ManageOwnersUrl { get; }
        public string DeleteUrl { get; }
        public bool CanEdit { get; }
        public bool CanManageOwners { get; }
        public bool CanDelete { get; }
        public bool CanEditRequiredSigner { get; }
        public bool ShowRequiredSigner { get; }
        public bool ShowTextBox { get; }
    }
}