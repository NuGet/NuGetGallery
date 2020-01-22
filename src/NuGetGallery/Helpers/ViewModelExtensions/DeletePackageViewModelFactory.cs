// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class DeletePackageViewModelFactory
    {
        private readonly DisplayPackageViewModelFactory _displayPackageViewModelFactory;

        public DeletePackageViewModelFactory(IIconUrlProvider iconUrlProvider)
        {
            _displayPackageViewModelFactory = new DisplayPackageViewModelFactory(iconUrlProvider);
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