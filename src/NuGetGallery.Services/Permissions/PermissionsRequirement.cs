// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    /// <summary>
    /// Represents a permissions restriction applied to a <see cref="User"/> who is trying to do an operation <see cref="User"/> has with a resource such as a <see cref="Package"/> or account (e.g. another <see cref="User"/>).
    /// </summary>
    [Flags]
    public enum PermissionsRequirement
    {
        /// <summary>
        /// No user can satisfy the requirement.
        /// </summary>
        Unsatisfiable = 0,

        /// <summary>
        /// No requirement--any user can satisfy the requirement.
        /// </summary>
        None = 1 << 0,

        /// <summary>
        /// If the user is an owner of the <see cref="Package"/>, <see cref="User"/>, or <see cref="ReservedNamespace"/>, they satisfy the requirement.
        /// </summary>
        Owner = 1 << 1,

        /// <summary>
        /// If the user is an admin of the site (see <see cref="GalleryConstants.AdminRoleName"/>), they satisfy the requirement.
        /// </summary>
        SiteAdmin = 1 << 2,

        /// <summary>
        /// If the user is an admin of an <see cref="Organization"/> (<see cref="Membership.IsAdmin"/> is true) that satisfies <see cref="Owner"/>, they satisfy the requirement.
        /// </summary>
        OrganizationAdmin = 1 << 3,

        /// <summary>
        /// If the user is a collaborator of an <see cref="Organization"/> (<see cref="Membership.IsAdmin"/> is false) that satisfies <see cref="Owner"/>, they satisfy the requirement.
        /// </summary>
        OrganizationCollaborator = 1 << 4,
    }
}