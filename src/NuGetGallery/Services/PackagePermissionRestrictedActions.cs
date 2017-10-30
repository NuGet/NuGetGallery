// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public static class PackagePermissionRestrictedActions
    {
        public static IPermissionRestrictedAction AcceptOwnership =
            new PermissionRestrictedActionExcludeLevel(new PermissionLevel[]
            {
                PermissionLevel.Owner,
            });

        public static IPermissionRestrictedAction DisplayPrivatePackage =
            new PermissionRestrictedActionIncludeLevel(new PermissionLevel[]
            {
                PermissionLevel.Owner,
                PermissionLevel.OrganizationAdmin,
                PermissionLevel.SiteAdmin,
                PermissionLevel.OrganizationCollaborator,
            });

        public static IPermissionRestrictedAction UploadNewVersion =
            new PermissionRestrictedActionIncludeLevel(new PermissionLevel[]
            {
                PermissionLevel.Owner,
                PermissionLevel.OrganizationAdmin,
                PermissionLevel.SiteAdmin,
                PermissionLevel.OrganizationCollaborator,
            });

        public static IPermissionRestrictedAction Edit =
            new PermissionRestrictedActionIncludeLevel(new PermissionLevel[]
            {
                PermissionLevel.Owner,
                PermissionLevel.OrganizationAdmin,
                PermissionLevel.SiteAdmin,
                PermissionLevel.OrganizationCollaborator,
            });

        public static IPermissionRestrictedAction Unlist =
            new PermissionRestrictedActionIncludeLevel(new PermissionLevel[]
            {
                PermissionLevel.Owner,
                PermissionLevel.OrganizationAdmin,
                PermissionLevel.SiteAdmin,
                PermissionLevel.OrganizationCollaborator,
            });

        public static IPermissionRestrictedAction ManagePackageOwners =
            new PermissionRestrictedActionIncludeLevel(new PermissionLevel[]
            {
                PermissionLevel.Owner,
                PermissionLevel.OrganizationAdmin,
                PermissionLevel.SiteAdmin,
            });

        public static IPermissionRestrictedAction ReportMyPackage =
            new PermissionRestrictedActionIncludeLevel(new PermissionLevel[]
            {
                PermissionLevel.Owner,
                PermissionLevel.OrganizationAdmin,
            });
    }
}