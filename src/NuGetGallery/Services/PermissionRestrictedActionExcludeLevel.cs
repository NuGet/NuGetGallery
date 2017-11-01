// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    /// <summary>
    /// An action that is allowed if the user does not have any of the <see cref="PermissionLevel"/>s in a given set.
    /// </summary>
    public class PermissionRestrictedActionExcludeLevel : IPermissionRestrictedAction
    {
        private HashSet<PermissionLevel> _excludedPermissionLevels;

        public PermissionRestrictedActionExcludeLevel(IEnumerable<PermissionLevel> excludedPermissionLevels)
        {
            _excludedPermissionLevels = new HashSet<PermissionLevel>(excludedPermissionLevels);
        }

        public bool IsAllowed(IEnumerable<PermissionLevel> permissionLevels)
        {
            return !permissionLevels.Any(permissionLevel => _excludedPermissionLevels.Contains(permissionLevel));
        }
    }
}
