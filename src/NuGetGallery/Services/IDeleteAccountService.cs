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
        /// 2. Any of the packages that become orphaned as its result will be unlisted if the unlistOrphanPackages is set to true.
        /// 3. Any owned namespaces will be released.
        /// 4. The user credentials will be cleaned.
        /// 5. The user data will be cleaned.
        /// </summary>
        /// <param name="userToBeDeleted">The user to be deleted.</param>
        /// <param name="admin">The admin that will perform the delete action.</param>
        /// <param name="signature">The admin signature.</param>
        /// <param name="unlistOrphanPackages">If the orphaned packages will unlisted.</param>
        /// <param name="commitAsTransaction">If the data will be persisted as a transaction.</param>
        Task<DeleteUserAccountStatus> DeleteGalleryUserAccountAsync(User userToBeDeleted, User admin, string signature, bool unlistOrphanPackages, bool commitAsTransaction);

        /// <summary>
        /// Will clean-up the data related with an organization account.
        /// The result will be:
        /// 1. The organization will be removed as owner from its owned packages.
        /// 2. Any owned namespaces will be released.
        /// 3. The organization's data will be cleaned.
        /// </summary>
        /// <param name="userToBeDeleted">The user to be deleted.</param>
        /// <param name="requestingUser">The user that requested the delete action.</param>
        /// <param name="commitAsTransaction">If the data will be persisted as a transaction.</param>
        Task<DeleteUserAccountStatus> DeleteGalleryOrganizationAccountAsync(Organization organizationToBeDeleted, User requestingUser, bool commitAsTransaction);
    }
}
