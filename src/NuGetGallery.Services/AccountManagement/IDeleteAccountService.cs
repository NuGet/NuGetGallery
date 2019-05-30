// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IDeleteAccountService
    {
        /// <summary>
        /// Will clean-up the data related with an user account.
        /// The result will be:
        /// 1. The user will be removed as owner from its owned packages.
        /// 2. Any of the packages that become orphaned as its result will be handled according to <paramref name="orphanPackagePolicy"/>.
        /// 3. Any owned namespaces will be released.
        /// 4. The user credentials will be cleaned.
        /// 5. The user data will be cleaned.
        /// </summary>
        /// <param name="userToBeDeleted">The user to be deleted.</param>
        /// <param name="userToExecuteTheDelete">The user deleting the account.</param>
        /// <param name="orphanPackagePolicy">If deleting the account creates any orphaned packages, a <see cref="AccountDeletionOrphanPackagePolicy"/> that describes how those orphans should be handled.</param>
        Task<DeleteAccountStatus> DeleteAccountAsync(User userToBeDeleted,
            User userToExecuteTheDelete,
            AccountDeletionOrphanPackagePolicy orphanPackagePolicy = AccountDeletionOrphanPackagePolicy.DoNotAllowOrphans);
    }
}
