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

        public DeletePackageViewModelFactory()
        {
            _displayPackageViewModelFactory = new DisplayPackageViewModelFactory();
        }

        public DeletePackageViewModel Create(
            Package package,
            User currentUser,
            IReadOnlyList<ReportPackageReason> reasons)
        {
            var viewModel = new DeletePackageViewModel();
            return Setup(viewModel, package, currentUser, reasons);
        }

        public DeletePackageViewModel Setup(
            DeletePackageViewModel viewModel,
            Package package,
            User currentUser,
            IReadOnlyList<ReportPackageReason> reasons)
        {
            _displayPackageViewModelFactory.Setup(viewModel, package, currentUser, deprecation: null, readMeHtml: null);
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