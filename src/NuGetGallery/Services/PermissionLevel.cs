// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    /// <summary>
    /// Represents the level of access a <see cref="User"/> has with a resource such as a <see cref="Package"/> or account (e.g. another <see cref="User"/>).
    /// </summary>
    [Flags]
    public enum PermissionLevel
    {
        /// <summary>
        /// The default rights, held by all users.
        /// </summary>
        Anonymous = 1,

        /// <summary>
        /// The user is a direct owner of the package or user.
        /// </summary>
        Owner = 2,

        /// <summary>
        /// The user is a site admin and has administrative permissions.
        /// </summary>
        SiteAdmin = 4,

        /// <summary>
        /// The user is an administrator of an organization that has <see cref="Owner"/>.
        /// </summary>
        OrganizationAdmin = 8,

        /// <summary>
        /// The user is a collaborator of an organization that has <see cref="Owner"/>.
        /// </summary>
        OrganizationCollaborator = 16,
    }
}