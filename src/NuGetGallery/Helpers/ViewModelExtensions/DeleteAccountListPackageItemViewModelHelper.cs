// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class DeleteAccountListPackageItemViewModelHelper
    {
        private readonly ListPackageItemViewModelHelper _listPackageItemViewModelHelper;
        private readonly IPackageService _packageService;

        public DeleteAccountListPackageItemViewModelHelper(IPackageService packageService)
        {
            _listPackageItemViewModelHelper = new ListPackageItemViewModelHelper();
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
        }

        public DeleteAccountListPackageItemViewModel CreateDeleteAccountListPackageItemViewModel(Package package, User userToDelete, User currentUser)
        {
            var viewModel = new DeleteAccountListPackageItemViewModel();
            return SetupDeleteAccountListPackageItemViewModel(viewModel, package, userToDelete, currentUser);
        }

        private DeleteAccountListPackageItemViewModel SetupDeleteAccountListPackageItemViewModel(DeleteAccountListPackageItemViewModel viewModel, Package package, User userToDelete, User currentUser)
        {
            _listPackageItemViewModelHelper.SetupListPackageItemViewModel(viewModel, package, currentUser);
            return SetupDeleteAccountListPackageItemViewModelInternal(viewModel, package, userToDelete);
        }

        private DeleteAccountListPackageItemViewModel SetupDeleteAccountListPackageItemViewModelInternal(DeleteAccountListPackageItemViewModel viewModel, Package package, User userToDelete)
        {
            viewModel.WillBeOrphaned = _packageService.WillPackageBeOrphanedIfOwnerRemoved(package.PackageRegistration, userToDelete);
            return viewModel;
        }
    }
}