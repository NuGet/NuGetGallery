// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public static class ActionName
    {
        public const string AddOrganizationPost = "Add";
        public const string AdminClearContentCache = "ClearContentCache";
        public const string AdminDeleteAccountIndex = "Index";
        public const string AdminDeleteAccountSearch = "Search";
        public const string AdminDeletePackageSearch = "Search";
        public const string AdminDeletePackageIndex = "Index";
        public const string AdminDeletePackageReflow = "Reflow";
        public const string AdminDeletePackageReflowBulk = "ReflowBulk";
        public const string AdminDeletePackageReflowBulkConfirm = "ReflowBulkConfirm";
        public const string AdminFeatureFlags = "Index";
        public const string AdminHomeIndex = "Index";
        public const string AdminLockPackageIndex = "Index";
        public const string AdminLockPackageSearch = "Search";
        public const string AdminLockPackageUpdate = "Update";
        public const string AdminLuceneIndex = "Index";
        public const string AdminLuceneRebuild = "Rebuild";
        public const string AdminReservedNamespaceAddOwner = "AddOwner";
        public const string AdminReservedNamespaceAddNamespace = "AddNamespace";
        public const string AdminReservedNamespaceIndex = "Index";
        public const string AdminReservedNamespaceRemoveOwner = "RemoveOwner";
        public const string AdminReservedNamespaceRemoveNamespace = "RemoveNamespace";
        public const string AdminReservedNamespaceSearchPrefix = "SearchPrefix";
        public const string AdminRevalidationIndex = "Index";
        public const string AdminSecurityPolicyIndex = "Index";
        public const string AdminSecurityPolicySearch = "Search";
        public const string AdminSecurityPolicyUpdate = "Update";
        public const string AdminSupportRequestIndex = "Index";
        public const string AdminSupportRequestAddAdmin = "AddAdmin";
        public const string AdminSupportRequestDisableAdmin = "DisableAdmin";
        public const string AdminSupportRequestEnableAdmin = "EnableAdmin";
        public const string AdminSupportRequestFilter = "Filter";
        public const string AdminSupportRequestGetAdmins = "GetAdmins";
        public const string AdminSupportRequestHistory = "History";
        public const string AdminSupportRequestManageAdmins = "Admins";
        public const string AdminSupportRequestSave = "Save";
        public const string AdminSupportRequestUpdateAdmin = "UpdateAdmin";
        public const string AdminValidationIndex = "Index";
        public const string AdminValidationSearch = "Search";
        public const string Authenticate = "Authenticate";
        public const string AuthenticateExternal = "AuthenticateExternal";
        public const string CancelChangeEmail = "CancelChangeEmail";
        public const string ChangeEmail = "ChangeEmail";
        public const string ChangeEmailSubscription = "ChangeEmailSubscription";
        public const string ChangeMultiFactorAuthentication = "ChangeMultiFactorAuthentication";
        public const string ChangePassword = "ChangePassword";
        public const string ConfirmationRequired = "ConfirmationRequired";
        public const string ContactPagePost = "Contact";
        public const string CreatePackageVerificationKey = "CreatePackageVerificationKey";
        public const string CveIds = "CveIds";
        public const string CweIds = "CweIds";
        public const string DeletePackageUIPost = "Delete";
        public const string DeletePackageApi = "DeletePackageApi";
        public const string DeleteSymbolsPackage = "DeleteSymbolsPackage";
        public const string DeleteUser = "Delete";
        public const string GetSymbolPackageApi = "GetSymbolPackageApi";
        public const string GetPackageApi = "GetPackageApi";
        public const string GetNuGetExeApi = "GetNuGetExeApi";
        public const string HealthProbeApi = "HealthProbeApi";
        public const string LinkOrChangeExternalCredential = "LinkOrChangeExternalCredential";
        public const string PackageIDs = "PackageIDs";
        public const string PackageVersions = "PackageVersions";
        public const string PublishPackageApi = "PublishPackageApi";
        public const string PushPackageApi = "PushPackageApi";
        public const string PushSymbolPackageApi = "PushSymbolPackageApi";
        public const string Query = "Query";
        public const string RegisterUser = "Register";
        public const string RequestOrganizationAccountDeletionPost = "RequestAccountDeletion";
        public const string RequestUserAccountDeletionPost = "RequestAccountDeletion";
        public const string SetKillswitch = "SetKillswitch";
        public const string SignIn = "SignIn";
        public const string StatisticsDownloadsApi = "StatisticsDownloadsApi";
        public const string StatusApi = "StatusApi";
        public const string TransformToOrganization = "TransformToOrganization";
        public const string TransformToOrganizationConfirmation = "ConfirmTransform";
        public const string TransformToOrganizationRejection = "RejectTransform";
        public const string TransformToOrganizationCancellation = "CancelTransform";
        public const string UpdatePackageListed = "UpdateListed";
        public const string VerifyPackageKey = "VerifyPackageKey";
    }
}