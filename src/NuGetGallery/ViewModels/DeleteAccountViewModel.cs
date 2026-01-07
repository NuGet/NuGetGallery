// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class DeleteAccountViewModel : IDeleteAccountViewModel
    {
        public DeleteAccountViewModel(
            User userToDelete,
            IReadOnlyCollection<DeleteAccountListPackageItemViewModel> ownedPackages)
        {
            User = userToDelete;

            Packages = ownedPackages;

            HasPackagesThatWillBeOrphaned = Packages.Any(p => p.WillBeOrphaned);
        }

        public IReadOnlyCollection<DeleteAccountListPackageItemViewModel> Packages { get; }

        public User User { get; }

        public string AccountName => User.Username;

        public bool HasPackagesThatWillBeOrphaned { get; }
    }

    public class DeleteAccountListPackageItemViewModel : ListPackageItemViewModel
    {
        public bool WillBeOrphaned { get; set; }
    }

    public interface IDeleteAccountViewModel
    {
        string AccountName { get; }

        bool HasPackagesThatWillBeOrphaned { get; }
    }
}