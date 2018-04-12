// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGetGallery.Areas.Admin.ViewModels;

namespace NuGetGallery
{
    public interface IDeleteAccountService
    {
        /// <summary>
        /// Will clean-up the data related with an user account.
        /// The result will be:
        /// 1. The user will be removed as owner from its owned packages.
        /// 2. Any of the packages owned only by the user will be unlisted if <paramref name="unlistOrphanPackages"/> is set to true.
        /// 3. Any owned namespaces will be released.
        /// 4. Any organization memberships will be removed.
        /// 5. The user credentials will be cleaned.
        /// 6. The user data will be cleaned.
        /// </summary>
        /// <param name="userToBeDeleted">The user to be deleted.</param>
        /// <param name="admin">The admin that will perform the delete action.</param>
        /// <param name="signature">The admin signature.</param>
        /// <param name="unlistOrphanPackages">If the orphaned packages will unlisted.</param>
        /// <param name="commitAsTransaction">If the data will be persisted as a transaction.</param>
        /// <returns></returns>
        Task<DeleteUserAccountStatus> DeleteGalleryUserAccountAsync(User userToBeDeleted, User admin, string signature, bool unlistOrphanPackages, bool commitAsTransaction);
    }
}
