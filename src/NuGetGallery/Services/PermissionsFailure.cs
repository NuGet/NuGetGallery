// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// Describes the reason why an action from <see cref="ActionsRequiringPermissions"/> is not allowed.
    /// </summary>
    public enum PermissionsFailure
    {
        /// <summary>
        /// There is no failure. The action is allowed.
        /// </summary>
        None = default(int),

        /// <summary>
        /// The cause of failure is unknown.
        /// </summary>
        Unknown,

        /// <summary>
        /// The current user does not have permissions to perform the action on the <see cref="User"/>.
        /// </summary>
        Account,

        /// <summary>
        /// The current user does not have permissions to perform the action on the <see cref="NuGetGallery.PackageRegistration"/> on behalf of another <see cref="User"/>.
        /// </summary>
        PackageRegistration,

        /// <summary>
        /// The current user does not have permissions to perform the action on the <see cref="NuGetGallery.ReservedNamespace"/> on behalf of another <see cref="User"/>.
        /// </summary>
        ReservedNamespace
    }
}