// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGetGallery.AccountDeleter
{
    public interface IAccountManager
    {
        /// <summary>
        /// Try to delete the account of the specified username
        /// </summary>
        /// <param name="username"></param>
        /// <returns>True if the account delete completes, false if account delete did not proceed for some reason.</returns>
        Task<bool> DeleteAccount(string username); //Maybe userkey here? or an internal structure. Depends on when we resolve the user name against DB. same below. We want to limit db lookups if possible (re: minimize calls to services where possible).

        /// <summary>
        /// Try to get a contact email address for a user
        /// </summary>
        /// <param name="username"></param>
        /// <returns>An email address. Throw <see cref="EmailContactNotAllowedException"/> if email contact was disallowed</returns>
        Task<string> GetEmailAddresForUser(string username);
    }
}
