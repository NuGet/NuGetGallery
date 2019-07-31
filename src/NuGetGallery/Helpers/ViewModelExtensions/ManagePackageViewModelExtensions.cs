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
    public static class ManagePackageViewModelExtensions
    {
        public static ManagePackageViewModel Setup(
            this ManagePackageViewModel viewModel,
            Package package,
            User currentUser,
            IReadOnlyList<ReportPackageReason> reasons,
            UrlHelper url,
            string readMe,
            bool isManageDeprecationEnabled,
            IIconUrlProvider iconUrlProvider)
        {
            ((ListPackageItemViewModel)viewModel).Setup(package, currentUser, iconUrlProvider);

            viewModel.IsCurrentUserAnAdmin = currentUser != null && currentUser.IsAdministrator;

            viewModel.DeletePackagesRequest = new DeletePackagesRequest
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

            viewModel.IsLocked = package.PackageRegistration.IsLocked;

            viewModel.IsManageDeprecationEnabled = isManageDeprecationEnabled;

            var versionSelectPackages = package.PackageRegistration.Packages
                .Where(p => p.PackageStatusKey == PackageStatus.Available || p.PackageStatusKey == PackageStatus.Validating)
                .OrderByDescending(p => new NuGetVersion(p.Version))
                .ToList();

            var versionSelectList = new List<SelectListItem>();
            viewModel.VersionSelectList = versionSelectList;
            var versionListedStateDictionary = new Dictionary<string, ManagePackageViewModel.VersionListedState>();
            viewModel.VersionListedStateDictionary = versionListedStateDictionary;
            var versionReadMeStateDictionary = new Dictionary<string, ManagePackageViewModel.VersionReadMeState>();
            viewModel.VersionReadMeStateDictionary = versionReadMeStateDictionary;
            var versionDeprecationStateDictionary = new Dictionary<string, ManagePackageViewModel.VersionDeprecationState>();
            viewModel.VersionDeprecationStateDictionary = versionDeprecationStateDictionary;

            var submitUrlTemplate = url.PackageVersionActionTemplate("Edit");
            var getReadMeUrlTemplate = url.PackageVersionActionTemplate("GetReadMeMd");
            foreach (var versionSelectPackage in versionSelectPackages)
            {
                var text = PackageHelper.GetSelectListText(versionSelectPackage);
                var value = NuGetVersionFormatter.Normalize(versionSelectPackage.Version);
                versionSelectList.Add(new SelectListItem
                {
                    Text = text,
                    Value = value,
                    Selected = package == versionSelectPackage
                });

                versionListedStateDictionary.Add(
                    value,
                    new ManagePackageViewModel.VersionListedState(versionSelectPackage.Listed, versionSelectPackage.DownloadCount));

                var model = new TrivialPackageVersionModel(versionSelectPackage);
                versionReadMeStateDictionary.Add(
                    value,
                    new ManagePackageViewModel.VersionReadMeState(
                        submitUrlTemplate.Resolve(model),
                        getReadMeUrlTemplate.Resolve(model),
                        null));

                versionDeprecationStateDictionary.Add(
                    value,
                    GetVersionDeprecationState(versionSelectPackage.Deprecations.SingleOrDefault(), text));
            }

            // Update edit model with the readme.md data.
            viewModel.ReadMe = new EditPackageVersionReadMeRequest();
            if (package.HasReadMe)
            {
                viewModel.ReadMe.ReadMe.SourceType = ReadMeService.TypeWritten;
                viewModel.ReadMe.ReadMe.SourceText = readMe;
            }

            return viewModel;
        }

        private static ManagePackageViewModel.VersionDeprecationState GetVersionDeprecationState(
            PackageDeprecation deprecation,
            string text)
        {
            var result = new ManagePackageViewModel.VersionDeprecationState();

            result.Text = text;

            if (deprecation != null)
            {
                result.IsLegacy = deprecation.Status.HasFlag(PackageDeprecationStatus.Legacy);
                result.HasCriticalBugs = deprecation.Status.HasFlag(PackageDeprecationStatus.CriticalBugs);
                result.IsOther = deprecation.Status.HasFlag(PackageDeprecationStatus.Other);

                result.AlternatePackageId = deprecation.AlternatePackageRegistration?.Id;

                var alternatePackage = deprecation.AlternatePackage;
                if (alternatePackage != null)
                {
                    // A deprecation should not have both an alternate package registration and an alternate package.
                    // In case a deprecation does have both, we will hide the alternate package registration's ID in this model.
                    result.AlternatePackageId = alternatePackage?.Id;
                    result.AlternatePackageVersion = alternatePackage?.Version;
                }

                result.CustomMessage = deprecation.CustomMessage;
            }

            return result;
        }
    }
}