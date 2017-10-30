// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public enum PermissionLevel
    {
        /// <summary>
        /// The default rights, held by all users.
        /// </summary>
        Anonymous,

        /// <summary>
        /// The user is a direct owner.
        /// </summary>
        Owner,

        /// <summary>
        /// The user is a site admin and has administrative permissions.
        /// </summary>
        SiteAdmin,

        /// <summary>
        /// The user is an administrator of an organization that is a direct owner.
        /// </summary>
        OrganizationAdmin,

        /// <summary>
        /// The user is a collaborator of an organization that is a direct owner.
        /// </summary>
        OrganizationCollaborator,
    }
}