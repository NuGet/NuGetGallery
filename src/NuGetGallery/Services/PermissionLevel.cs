// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public enum PermissionLevel
    {
        /// <summary>
        /// The default rights to a package, held by all users on every package.
        /// </summary>
        Anonymous,

        /// <summary>
        /// The user is a direct owner of the package.
        /// </summary>
        Owner,

        /// <summary>
        /// The user is a site admin and has administrative permissions on all packages.
        /// </summary>
        SiteAdmin,

        /// <summary>
        /// The user is an administrator of an organization that is a direct owner of the package.
        /// </summary>
        OrganizationAdmin,

        /// <summary>
        /// The user is a collaborator of an organization that is a direct owner of the package.
        /// </summary>
        OrganizationCollaborator,
    }
}