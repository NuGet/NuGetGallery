// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class DeleteAccountViewModel<TAccount> : DeleteAccountViewModel where TAccount : User
    {
        public DeleteAccountViewModel(
            TAccount accountToDelete,
            User currentUser,
            IPackageService packageService)
            : base(accountToDelete, currentUser, packageService)
        {
            Account = accountToDelete;
        }

        public TAccount Account { get; set; }
    }

    public class DeleteAccountViewModel : IDeleteAccountViewModel
    {
        public DeleteAccountViewModel(
            User userToDelete,
            User currentUser,
            IPackageService packageService)
        {
            User = userToDelete;

            Packages = packageService
                 .FindPackagesByAnyMatchingOwner(User, includeUnlisted: true)
                 .Select(p => new DeleteAccountListPackageItemViewModel(p, userToDelete, currentUser, packageService))
                 .ToList();

            HasPackagesThatWillBeOrphaned = Packages.Any(p => p.WillBeOrphaned);
        }

        public List<DeleteAccountListPackageItemViewModel> Packages { get; }

        public User User { get; }

        public string AccountName => User.Username;

        public bool HasPackagesThatWillBeOrphaned { get; }
    }

    public class DeleteAccountListPackageItemViewModel : ListPackageItemViewModel
    {
        public DeleteAccountListPackageItemViewModel(
            Package package, 
            User userToDelete, 
            User currentUser, 
            IPackageService packageService)
        {
            // TODO: remove
            this.Setup(package, userToDelete, currentUser, packageService);
        }

        public bool WillBeOrphaned { get; set; }
    }

    public interface IDeleteAccountViewModel
    {
        string AccountName { get; }

        bool HasPackagesThatWillBeOrphaned { get; }
    }
}