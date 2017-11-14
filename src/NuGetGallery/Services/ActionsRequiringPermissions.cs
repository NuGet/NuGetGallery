// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// A collection of all <see cref="ActionRequiringAccountPermissions"/> and <see cref="IActionRequiringEntityPermissions{TEntity}"/>s.
    /// </summary>
    public static class ActionsRequiringPermissions
    {
        /// <summary>
        /// The action of seeing private metadata about a package.
        /// For example, if a package is validating, only users who can perform this action can see the metadata of the package.
        /// </summary>
        public static ActionRequiringPackagePermissions DisplayPrivatePackageMetadata =
            new ActionRequiringPackagePermissions(
                PermissionsRequirement.Owner | PermissionsRequirement.OrganizationAdmin | PermissionsRequirement.OrganizationCollaborator,
                PermissionsRequirement.Owner | PermissionsRequirement.SiteAdmin);

        /// <summary>
        /// The action of uploading a new package ID.
        /// </summary>
        public static ActionRequiringReservedNamespacePermissions UploadNewPackageId =
            new ActionRequiringReservedNamespacePermissions(
                PermissionsRequirement.Owner | PermissionsRequirement.OrganizationAdmin,
                PermissionsRequirement.Owner);

        /// <summary>
        /// The action of uploading a new version of an existing package ID.
        /// </summary>
        public static ActionRequiringPackagePermissions UploadNewPackageVersion =
            new ActionRequiringPackagePermissions(
                PermissionsRequirement.Owner | PermissionsRequirement.OrganizationAdmin | PermissionsRequirement.OrganizationCollaborator,
                PermissionsRequirement.Owner | PermissionsRequirement.SiteAdmin);

        /// <summary>
        /// The action of verify a package verification key.
        /// </summary>
        public static ActionRequiringPackagePermissions VerifyPackage =
            new ActionRequiringPackagePermissions(
                PermissionsRequirement.Owner | PermissionsRequirement.OrganizationAdmin | PermissionsRequirement.OrganizationCollaborator,
                PermissionsRequirement.Owner);

        /// <summary>
        /// The action of editing an existing version of an existing package ID.
        /// </summary>
        public static ActionRequiringPackagePermissions EditPackage =
            new ActionRequiringPackagePermissions(
                PermissionsRequirement.Owner | PermissionsRequirement.OrganizationAdmin | PermissionsRequirement.OrganizationCollaborator,
                PermissionsRequirement.Owner | PermissionsRequirement.SiteAdmin);

        /// <summary>
        /// The action of unlisting or relisting an existing version of an existing package ID.
        /// </summary>
        public static ActionRequiringPackagePermissions UnlistOrRelistPackage =
            new ActionRequiringPackagePermissions(
                PermissionsRequirement.Owner | PermissionsRequirement.OrganizationAdmin | PermissionsRequirement.OrganizationCollaborator,
                PermissionsRequirement.Owner | PermissionsRequirement.SiteAdmin);

        /// <summary>
        /// The action of managing the ownership of an existing package ID.
        /// </summary>
        public static ActionRequiringPackagePermissions ManagePackageOwnership =
            new ActionRequiringPackagePermissions(
                PermissionsRequirement.Owner | PermissionsRequirement.OrganizationAdmin,
                PermissionsRequirement.Owner | PermissionsRequirement.SiteAdmin);

        /// <summary>
        /// The action of reporting an existing package ID as the owner of the package..
        /// </summary>
        public static ActionRequiringPackagePermissions ReportPackageAsOwner =
            new ActionRequiringPackagePermissions(
                PermissionsRequirement.Owner | PermissionsRequirement.OrganizationAdmin,
                PermissionsRequirement.Owner);

        /// <summary>
        /// The action of handling package ownership requests for a user to become an owner of a package.
        /// </summary>
        public static ActionRequiringAccountPermissions HandlePackageOwnershipRequest = 
            new ActionRequiringAccountPermissions(
                PermissionsRequirement.Owner | PermissionsRequirement.OrganizationAdmin);
    }
}