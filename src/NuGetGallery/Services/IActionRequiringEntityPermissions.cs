// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace NuGetGallery
{
    public interface IActionRequiringEntityPermissions<TEntity>
    {
        PermissionsFailure IsAllowed(User currentUser, User account, TEntity entity);
        PermissionsFailure IsAllowed(IPrincipal currentPrincipal, User account, TEntity entity);
        bool TryGetAccountsIsAllowedOnBehalfOf(User currentUser, TEntity entity, out IEnumerable<User> accountsAllowedOnBehalfOf);
    }
}