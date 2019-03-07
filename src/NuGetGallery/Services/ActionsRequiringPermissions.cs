// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// A collection of all <see cref="ActionRequiringAccountPermissions"/> and <see cref="IActionRequiringEntityPermissions{TEntity}"/>s.
    /// </summary>
    public static class ActionsRequiringPermissions
    {
        private const PermissionsRequirement RequireOwnerOrSiteAdmin = 
            PermissionsRequirement.Owner | PermissionsRequirement.SiteAdmin;
        private const PermissionsRequirement RequireOwnerOrSiteAdminOrOrganizationAdmin =
            PermissionsRequirement.Owner | PermissionsRequirement.SiteAdmin | PermissionsRequirement.OrganizationAdmin;
        private const PermissionsRequirement RequireOwnerOrOrganizationAdmin = 
            PermissionsRequirement.Owner | PermissionsRequirement.OrganizationAdmin;
        private const PermissionsRequirement RequireOwnerOrOrganizationMember =
            PermissionsRequirement.Owner | PermissionsRequirement.OrganizationAdmin | PermissionsRequirement.OrganizationCollaborator;
        private const PermissionsRequirement RequireOwnerOrSiteAdminOrOrganizationMember =
            PermissionsRequirement.Owner | PermissionsRequirement.SiteAdmin | PermissionsRequirement.OrganizationAdmin | PermissionsRequirement.OrganizationCollaborator;

        /// <summary>
        /// The action of seeing private metadata about a package.
        /// For example, if a package is validating, only users who can perform this action can see the metadata of the package.
        /// </summary>
        public static ActionRequiringPackagePermissions DisplayPrivatePackageMetadata =
            new ActionRequiringPackagePermissions(
                accountOnBehalfOfPermissionsRequirement: RequireOwnerOrOrganizationMember,
                packageRegistrationPermissionsRequirement: RequireOwnerOrSiteAdmin);

        /// <summary>
        /// The action of uploading a new package ID.
        /// </summary>
        public static ActionRequiringReservedNamespacePermissions UploadNewPackageId =
            new ActionRequiringReservedNamespacePermissions(
                accountOnBehalfOfPermissionsRequirement: RequireOwnerOrOrganizationMember,
                reservedNamespacePermissionsRequirement: PermissionsRequirement.Owner);

        /// <summary>
        /// The action of uploading a new version of an existing package ID.
        /// </summary>
        public static ActionRequiringPackagePermissions UploadNewPackageVersion =
            new ActionRequiringPackagePermissions(
                accountOnBehalfOfPermissionsRequirement: RequireOwnerOrOrganizationMember,
                packageRegistrationPermissionsRequirement: PermissionsRequirement.Owner);

        /// <summary>
        /// The action of uploading a symbols package for an existing package.
        /// </summary>
        public static ActionRequiringPackagePermissions UploadSymbolPackage =
            new ActionRequiringPackagePermissions(
                accountOnBehalfOfPermissionsRequirement: RequireOwnerOrOrganizationMember,
                packageRegistrationPermissionsRequirement: PermissionsRequirement.Owner);

        /// <summary>
        /// The action of deleting a symbols package for an existing package.
        /// </summary>
        public static ActionRequiringPackagePermissions DeleteSymbolPackage =
            new ActionRequiringPackagePermissions(
                accountOnBehalfOfPermissionsRequirement: RequireOwnerOrOrganizationMember,
                packageRegistrationPermissionsRequirement: RequireOwnerOrSiteAdmin);

        /// <summary>
        /// The action of verify a package verification key.
        /// </summary>
        public static ActionRequiringPackagePermissions VerifyPackage =
            new ActionRequiringPackagePermissions(
                accountOnBehalfOfPermissionsRequirement: RequireOwnerOrOrganizationMember,
                packageRegistrationPermissionsRequirement: PermissionsRequirement.Owner);

        /// <summary>
        /// The action of editing an existing version of an existing package ID.
        /// </summary>
        public static ActionRequiringPackagePermissions EditPackage =
            new ActionRequiringPackagePermissions(
                accountOnBehalfOfPermissionsRequirement: RequireOwnerOrOrganizationMember,
                packageRegistrationPermissionsRequirement: RequireOwnerOrSiteAdmin);

        /// <summary>
        /// The action of unlisting or relisting an existing version of an existing package ID.
        /// </summary>
        public static ActionRequiringPackagePermissions UnlistOrRelistPackage =
            new ActionRequiringPackagePermissions(
                accountOnBehalfOfPermissionsRequirement: RequireOwnerOrOrganizationMember,
                packageRegistrationPermissionsRequirement: RequireOwnerOrSiteAdmin);

        /// <summary>
        /// The action of deprecating an existing version of an existing package ID.
        /// </summary>
        public static ActionRequiringPackagePermissions DeprecatePackage =
            new ActionRequiringPackagePermissions(
                accountOnBehalfOfPermissionsRequirement: RequireOwnerOrOrganizationMember,
                packageRegistrationPermissionsRequirement: RequireOwnerOrSiteAdmin);

        /// <summary>
        /// The action of managing the ownership of an existing package ID.
        /// </summary>
        public static ActionRequiringPackagePermissions ManagePackageOwnership =
            new ActionRequiringPackagePermissions(
                accountOnBehalfOfPermissionsRequirement: RequireOwnerOrOrganizationAdmin,
                packageRegistrationPermissionsRequirement: RequireOwnerOrSiteAdmin);

        /// <summary>
        /// The action of reporting an existing package ID as the owner of the package.
        /// </summary>
        public static ActionRequiringPackagePermissions ReportPackageAsOwner =
            new ActionRequiringPackagePermissions(
                accountOnBehalfOfPermissionsRequirement: RequireOwnerOrOrganizationMember,
                packageRegistrationPermissionsRequirement: PermissionsRequirement.Owner);

        /// <summary>
        /// The action of seeing a breadcrumb linking the user back to their profile when performing actions on a package.
        /// </summary>
        public static ActionRequiringPackagePermissions ShowProfileBreadcrumb =
            new ActionRequiringPackagePermissions(
                accountOnBehalfOfPermissionsRequirement: RequireOwnerOrOrganizationMember,
                packageRegistrationPermissionsRequirement: PermissionsRequirement.Owner);

        /// <summary>
        /// The action of handling package ownership requests for a user to become an owner of a package.
        /// </summary>
        public static ActionRequiringAccountPermissions HandlePackageOwnershipRequest = 
            new ActionRequiringAccountPermissions(
                accountPermissionsRequirement: RequireOwnerOrOrganizationAdmin);

        /// <summary>
        /// The action of viewing (read-only) a user or organization account.
        /// </summary>
        public static ActionRequiringAccountPermissions ViewAccount =
            new ActionRequiringAccountPermissions(
                accountPermissionsRequirement: RequireOwnerOrSiteAdminOrOrganizationMember);

        /// <summary>
        /// The action of managing a user or organization account. This includes confirming an account,
        /// changing the email address, changing email subscriptions, modifying sign-in credentials, etc.
        /// </summary>
        public static ActionRequiringAccountPermissions ManageAccount =
            new ActionRequiringAccountPermissions(
                accountPermissionsRequirement: RequireOwnerOrOrganizationAdmin);

        /// <summary>
        /// The action of managing an organization's memberships.
        /// </summary>
        public static ActionRequiringAccountPermissions ManageMembership =
            new ActionRequiringAccountPermissions(
                accountPermissionsRequirement: RequireOwnerOrSiteAdminOrOrganizationAdmin);

        /// <summary>
        /// The action of changing a package's required signer.
        /// </summary>
        public static ActionRequiringPackagePermissions ManagePackageRequiredSigner =
             new ActionRequiringPackagePermissions(
                accountOnBehalfOfPermissionsRequirement: RequireOwnerOrOrganizationAdmin,
                packageRegistrationPermissionsRequirement: RequireOwnerOrOrganizationAdmin);

        /// <summary>
        /// The action of adding a package to a reserved namespace that the package is in.
        /// </summary>
        public static ActionRequiringReservedNamespacePermissions AddPackageToReservedNamespace =
            new ActionRequiringReservedNamespacePermissions(
                accountOnBehalfOfPermissionsRequirement: PermissionsRequirement.Owner,
                reservedNamespacePermissionsRequirement: PermissionsRequirement.Owner);

        /// <summary>
        /// The action of removing a package from a reserved namespace that the package is in.
        /// </summary>
        public static ActionRequiringReservedNamespacePermissions RemovePackageFromReservedNamespace =
            new ActionRequiringReservedNamespacePermissions(
                accountOnBehalfOfPermissionsRequirement: RequireOwnerOrSiteAdminOrOrganizationAdmin,
                reservedNamespacePermissionsRequirement: RequireOwnerOrOrganizationAdmin);
    }
}