// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class DeletePackageViewModelHelper
    {
        private readonly DisplayPackageViewModelHelper _displayPackageViewModelHelper;

        public DeletePackageViewModelHelper()
        {
            _displayPackageViewModelHelper = new DisplayPackageViewModelHelper();
        }

        public DeletePackageViewModel CreateDeletePackageViewModel(
            Package package,
            User currentUser,
            IReadOnlyList<ReportPackageReason> reasons)
        {
            var viewModel = new DeletePackageViewModel();
            return SetupDeletePackageViewModel(viewModel, package, currentUser, reasons);
        }

        public DeletePackageViewModel SetupDeletePackageViewModel(
            DeletePackageViewModel viewModel,
            Package package,
            User currentUser,
            IReadOnlyList<ReportPackageReason> reasons)
        {
            _displayPackageViewModelHelper.SetupDisplayPackageViewModel(viewModel, package, currentUser, deprecation: null, readMeHtml: null);
            return SetupDeletePackageViewModelInternal(viewModel, package, reasons);
        }

        private DeletePackageViewModel SetupDeletePackageViewModelInternal(
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