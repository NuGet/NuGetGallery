// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Areas.Admin.Models;

namespace NuGetGallery
{
    public class DeleteAccountViewModel<TAccount> : DeleteAccountViewModel where TAccount : User
    {
        public DeleteAccountViewModel(
            TAccount accountToDelete,
            User currentUser,
            IPackageService packageService,
            Func<ListPackageItemViewModel, bool> packageIsOrphaned)
            : base(accountToDelete, currentUser, packageService, packageIsOrphaned)
        {
            Account = accountToDelete;
        }

        public TAccount Account { get; set; }
    }

    public class DeleteAccountViewModel : IDeleteAccountViewModel
    {
        private Lazy<bool> _hasOrphanPackages;

        public DeleteAccountViewModel(
            User userToDelete,
            User currentUser,
            IPackageService packageService,
            Func<ListPackageItemViewModel, bool> packageIsOrphaned)
        {
            User = userToDelete;

            Packages = packageService
                 .FindPackagesByAnyMatchingOwner(User, includeUnlisted: true)
                 .Select(p => new ListPackageItemViewModel(p, currentUser))
                 .ToList();

            _hasOrphanPackages = new Lazy<bool>(() => Packages.Any(packageIsOrphaned));
        }

        public List<ListPackageItemViewModel> Packages { get; }

        public User User { get; }

        public string AccountName => User.Username;

        public bool HasOrphanPackages
        {
            get
            {
                return Packages == null ? false : _hasOrphanPackages.Value;
            }
        }
    }

    public interface IDeleteAccountViewModel
    {
        string AccountName { get; }

        bool HasOrphanPackages { get; }
    }
}