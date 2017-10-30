// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public class PermissionRestrictedActionIncludeLevel : IPermissionRestrictedAction
    {
        private HashSet<PermissionLevel> _allowedPermissionLevels;

        public PermissionRestrictedActionIncludeLevel(IEnumerable<PermissionLevel> allowedPermissionLevels)
        {
            _allowedPermissionLevels = new HashSet<PermissionLevel>(allowedPermissionLevels);
        }

        public bool IsAllowed(IEnumerable<PermissionLevel> permissionLevels)
        {
            return permissionLevels.Any(permissionLevel => _allowedPermissionLevels.Contains(permissionLevel));
        }
    }
}
