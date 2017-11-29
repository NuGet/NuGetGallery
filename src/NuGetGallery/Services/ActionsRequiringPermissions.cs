// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public static class ActionsRequiringPermissions
    {
        public static ActionRequiringPackagePermissions DisplayPrivatePackageMetadata =
            new ActionRequiringPackagePermissions(
                PermissionsRequirement.Owner | PermissionsRequirement.OrganizationAdmin | PermissionsRequirement.OrganizationCollaborator,
                PermissionsRequirement.Owner | PermissionsRequirement.SiteAdmin);

        public static ActionRequiringReservedNamespacePermissions UploadNewPackageId =
            new ActionRequiringReservedNamespacePermissions(
                PermissionsRequirement.Owner | PermissionsRequirement.OrganizationAdmin,
                PermissionsRequirement.Owner);

        public static ActionRequiringPackagePermissions UploadNewPackageVersion =
            new ActionRequiringPackagePermissions(
                PermissionsRequirement.Owner | PermissionsRequirement.OrganizationAdmin | PermissionsRequirement.OrganizationCollaborator,
                PermissionsRequirement.Owner | PermissionsRequirement.SiteAdmin);

        public static ActionRequiringPackagePermissions EditPackage =
            new ActionRequiringPackagePermissions(
                PermissionsRequirement.Owner | PermissionsRequirement.OrganizationAdmin | PermissionsRequirement.OrganizationCollaborator,
                PermissionsRequirement.Owner | PermissionsRequirement.SiteAdmin);

        public static ActionRequiringPackagePermissions UnlistOrRelistPackage =
            new ActionRequiringPackagePermissions(
                PermissionsRequirement.Owner | PermissionsRequirement.OrganizationAdmin | PermissionsRequirement.OrganizationCollaborator,
                PermissionsRequirement.Owner | PermissionsRequirement.SiteAdmin);

        public static ActionRequiringPackagePermissions ManagePackageOwnership =
            new ActionRequiringPackagePermissions(
                PermissionsRequirement.Owner | PermissionsRequirement.OrganizationAdmin,
                PermissionsRequirement.Owner | PermissionsRequirement.SiteAdmin);

        public static ActionRequiringPackagePermissions ReportPackageAsOwner =
            new ActionRequiringPackagePermissions(
                PermissionsRequirement.Owner | PermissionsRequirement.OrganizationAdmin,
                PermissionsRequirement.Owner);

        public static ActionRequiringAccountPermissions HandlePackageOwnershipRequest = 
            new ActionRequiringAccountPermissions(
                PermissionsRequirement.Owner | PermissionsRequirement.OrganizationAdmin);

        public static ActionRequiringAccountPermissions DisplayPrivateOrganization =
            new ActionRequiringAccountPermissions(
                PermissionsRequirement.Owner | PermissionsRequirement.OrganizationAdmin);
    }
}