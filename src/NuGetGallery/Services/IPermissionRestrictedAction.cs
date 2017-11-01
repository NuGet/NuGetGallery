// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    /// <summary>
    /// An action that is restricted based on a user's <see cref="PermissionLevel"/>s.
    /// </summary>
    public interface IPermissionRestrictedAction
    {
        bool IsAllowed(IEnumerable<PermissionLevel> permissionLevel);
    }
}
