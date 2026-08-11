// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class StagingApiError
    {
        public StagingApiError(string code, string message, string target = null)
        {
            Code = code;
            Message = message;
            Target = target;
        }

        public string Code { get; }
        public string Message { get; }
        public string Target { get; }
    }

    public class StagingApiErrorResponse
    {
        public StagingApiErrorResponse(StagingApiError error)
        {
            Error = error;
        }

        public StagingApiError Error { get; }
    }

    public static class StagingApiErrorCodes
    {
        public const string InvalidMultipart = "InvalidMultipart";
        public const string InvalidPackage = "InvalidPackage";
        public const string ArtifactIdentityMismatch = "ArtifactIdentityMismatch";
        public const string InvalidGroupId = "InvalidGroupId";
        public const string InvalidRequestBody = "InvalidRequestBody";
        public const string InvalidContinuationToken = "InvalidContinuationToken";
        public const string InvalidFilterCombination = "InvalidFilterCombination";
        public const string AuthenticationRequired = "AuthenticationRequired";
        public const string StagingScopeRequired = "StagingScopeRequired";
        public const string StagedPackageNotFound = "StagedPackageNotFound";
        public const string GroupNotFound = "GroupNotFound";
        public const string ParentNotFound = "ParentNotFound";
        public const string PackageVersionConflict = "PackageVersionConflict";
        public const string StagedPackagePromoting = "StagedPackagePromoting";
        public const string GroupPromoting = "GroupPromoting";
        public const string GroupAlreadyExists = "GroupAlreadyExists";
        public const string GroupLimitExceeded = "GroupLimitExceeded";
        public const string ArtifactQuotaExceeded = "ArtifactQuotaExceeded";
        public const string RequestTooLarge = "RequestTooLarge";
        public const string RateLimitExceeded = "RateLimitExceeded";
        public const string StagingUnavailable = "StagingUnavailable";
    }
}
