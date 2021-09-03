// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Areas.Admin.ViewModels
{
    /// <summary>
    /// The possible ways that a user have have ownership change with respect to a package registration. These enum
    /// values are ordered in such a way that ownership additions are performed prior to ownership removal.
    /// </summary>
    public enum PackageOwnershipState
    {
        /// <summary>
        /// This user is an owner of the package and will not be modified.
        /// </summary>
        ExistingOwner,

        /// <summary>
        /// This user has an existing ownership request for the package and will not be modified.
        /// </summary>
        ExistingOwnerRequest,

        /// <summary>
        /// This user is already an owner of the package so no request needs to be sent.
        /// </summary>
        AlreadyOwner,

        /// <summary>
        /// This user already has an existing ownership request for the package so no request needs to be sent.
        /// </summary>
        AlreadyOwnerRequest,

        /// <summary>
        /// This user is not currently an owner but the requestor has access to this user so it will be immediately added as an owner.
        /// </summary>
        NewOwner,

        /// <summary>
        /// This user is not currently an owner so an ownership request will be sent.
        /// </summary>
        NewOwnerRequest,

        /// <summary>
        /// This user is currently an owner of the package and will be removed.
        /// </summary>
        RemoveOwner,

        /// <summary>
        /// This user currently has an ownership request which will be removed.
        /// </summary>
        RemoveOwnerRequest,

        /// <summary>
        /// This user is not currently an owner and does not have an ownership request. No change will occur for this package and user combination.
        /// </summary>
        RemoveNoOp,
    }
}