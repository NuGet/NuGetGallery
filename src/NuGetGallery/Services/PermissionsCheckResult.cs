// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// Describes the result of checking if an <see cref="ActionsRequiringPermissions"/> is allowed.
    /// </summary>
    public enum PermissionsCheckResult
    {
        /// <summary>
        /// The action is allowed.
        /// </summary>
        Allowed = default(int),

        /// <summary>
        /// The permissions check failed for an unknown reason.
        /// </summary>
        Unknown,

        /// <summary>
        /// The current user does not have permissions to perform the action on the <see cref="User"/>.
        /// </summary>
        AccountFailure,

        /// <summary>
        /// The current user does not have permissions to perform the action on the <see cref="PackageRegistration"/> on behalf of another <see cref="User"/>.
        /// </summary>
        PackageRegistrationFailure,

        /// <summary>
        /// The current user does not have permissions to perform the action on the <see cref="ReservedNamespace"/> on behalf of another <see cref="User"/>.
        /// </summary>
        ReservedNamespaceFailure
    }
}