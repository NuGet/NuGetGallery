// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using NuGet.Services.Entities;
using NuGetGallery.Frameworks;

namespace NuGetGallery
{
    public class DeletePackageViewModelFactory
    {
        private readonly DisplayPackageViewModelFactory _displayPackageViewModelFactory;

        public DeletePackageViewModelFactory(IIconUrlProvider iconUrlProvider, IPackageFrameworkCompatibilityFactory compatibilityFactory, IFeatureFlagService featureFlagService)
        {
            _displayPackageViewModelFactory = new DisplayPackageViewModelFactory(iconUrlProvider, compatibilityFactory, featureFlagService);
        }

        public DeletePackageViewModel Create(
            Package package,
            IReadOnlyCollection<Package> allVersions,
            User currentUser,
            IReadOnlyList<ReportPackageReason> reasons)
        {
            var viewModel = new DeletePackageViewModel();
            return Setup(viewModel, package, allVersions, currentUser, reasons);
        }

        public DeletePackageViewModel Setup(
            DeletePackageViewModel viewModel,
            Package package,
            IReadOnlyCollection<Package> allVersions,
            User currentUser,
            IReadOnlyList<ReportPackageReason> reasons)
        {
            _displayPackageViewModelFactory.Setup(
                viewModel,
                package,
                allVersions,
                currentUser,
                packageKeyToDeprecation: null,
                packageKeyToVulnerabilities: null,
                packageRenames: null,
                readmeResult: null);

            return SetupInternal(viewModel, package, reasons);
        }

        private DeletePackageViewModel SetupInternal(
            DeletePackageViewModel viewModel,
            Package package,
            IReadOnlyList<ReportPackageReason> reasons)
        {
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

            return viewModel;
        }
    }
}