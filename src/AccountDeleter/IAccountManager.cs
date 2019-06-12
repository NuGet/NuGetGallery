// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.AccountDeleter
{
    public interface IAccountManager
    {
        /// <summary>
        /// Try to delete the account of the specified username
        /// </summary>
        /// <param name="username"></param>
        /// <returns>True if the account delete completes, false if account delete did not proceed for some reason.</returns>
        bool DeleteAccount(string username); //Maybe userkey here? or an internal structure. Depends on when we resolve the user name against DB. same below. We want to limit db lookups if possible (re: minimize calls to services where possible).
    }
}
