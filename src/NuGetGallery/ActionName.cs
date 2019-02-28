// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public static class ActionName
    {
        public const string Authenticate = "Authenticate";
        public const string AuthenticateExternal = "AuthenticateExternal";
        public const string ConfirmationRequired = "ConfirmationRequired";
        public const string CreatePackageVerificationKey = "CreatePackageVerificationKey";
        public const string CveIds = "CveIds";
        public const string CweIds = "CweIds";
        public const string DeletePackageApi = "DeletePackageApi";
        public const string GetSymbolPackageApi = "GetSymbolPackageApi";
        public const string GetPackageApi = "GetPackageApi";
        public const string GetNuGetExeApi = "GetNuGetExeApi";
        public const string HealthProbeApi = "HealthProbeApi";
        public const string PackageIDs = "PackageIDs";
        public const string PackageVersions = "PackageVersions";
        public const string PublishPackageApi = "PublishPackageApi";
        public const string PushPackageApi = "PushPackageApi";
        public const string PushSymbolPackageApi = "PushSymbolPackageApi";
        public const string Query = "Query";
        public const string StatisticsDownloadsApi = "StatisticsDownloadsApi";
        public const string StatusApi = "StatusApi";
        public const string TransformToOrganization = "TransformToOrganization";
        public const string TransformToOrganizationConfirmation = "ConfirmTransform";
        public const string TransformToOrganizationRejection = "RejectTransform";
        public const string TransformToOrganizationCancellation = "CancelTransform";
        public const string VerifyPackageKey = "VerifyPackageKey";
    }
}