// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery
{
    public static class DeleteAccountListPackageItemViewModelExtensions
    {
        public static DeleteAccountListPackageItemViewModel Setup(
            this DeleteAccountListPackageItemViewModel viewModel,
            Package package,
            User userToDelete,
            User currentUser,
            IPackageService packageService)
        {
            ((ListPackageItemViewModel)viewModel).Setup(package, currentUser);
            viewModel.WillBeOrphaned = packageService.WillPackageBeOrphanedIfOwnerRemoved(package.PackageRegistration, userToDelete);
            return viewModel;
        }
    }
}