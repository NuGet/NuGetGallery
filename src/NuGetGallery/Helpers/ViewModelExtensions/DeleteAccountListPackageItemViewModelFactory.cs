﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class DeleteAccountListPackageItemViewModelFactory
    {
        private readonly ListPackageItemViewModelFactory _listPackageItemViewModelFactory;
        private readonly IPackageService _packageService;

        public DeleteAccountListPackageItemViewModelFactory(IPackageService packageService, IIconUrlProvider iconUrlProvider)
        {
            _listPackageItemViewModelFactory = new ListPackageItemViewModelFactory(iconUrlProvider);
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
        }

        public DeleteAccountListPackageItemViewModel Create(Package package, User userToDelete, User currentUser)
        {
            var viewModel = new DeleteAccountListPackageItemViewModel();
            return Setup(viewModel, package, userToDelete, currentUser);
        }

        private DeleteAccountListPackageItemViewModel Setup(DeleteAccountListPackageItemViewModel viewModel, Package package, User userToDelete, User currentUser)
        {
            _listPackageItemViewModelFactory.Setup(viewModel, package, currentUser);
            return SetupInternal(viewModel, package, userToDelete);
        }

        private DeleteAccountListPackageItemViewModel SetupInternal(DeleteAccountListPackageItemViewModel viewModel, Package package, User userToDelete)
        {
            viewModel.WillBeOrphaned = _packageService.WillPackageBeOrphanedIfOwnerRemoved(package.PackageRegistration, userToDelete);
            return viewModel;
        }
    }
}