// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGetGallery.Areas.Admin.ViewModels;

namespace NuGetGallery
{
    public interface IDeleteAccountService
    {
        /// <summary>
        /// Deletes an user gallery account.
        /// </summary>
        /// <param name="userToBeDeleted">The user to be deleted.</param>
        /// <param name="admin">The admin that will execute the delete action.</param>
        /// <param name="signature">The admin signature.</param>
        /// <param name="unsignOrphanPackages">True if the orphan packages will be unlisted.</param>
        /// <param name="commitAsTransaction">True if the changes will commited as a transaction.</param>
        /// <returns></returns>
        Task<DeleteUserAccountStatus> DeleteGalleryUserAccountAsync(User userToBeDeleted, User admin, string signature, bool unsignOrphanPackages, bool commitAsTransaction);
    }
}
