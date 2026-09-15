// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public static class StagingApiErrorCodes
    {
        public const string GroupNotFound = "GroupNotFound";
        public const string InvalidGroupId = "InvalidGroupId";
        public const string PackageUploadFailed = "PackageUploadFailed";
    }

    public class PackageStagingResult
    {
        private PackageStagingResult(
            HttpStatusCode statusCode,
            StagedPackage stagedPackage,
            string errorCode,
            string errorMessage,
            string errorTarget,
            IReadOnlyList<IValidationMessage> warnings)
        {
            StatusCode = statusCode;
            StagedPackage = stagedPackage;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            ErrorTarget = errorTarget;
            Warnings = warnings ?? Array.Empty<IValidationMessage>();
        }

        public HttpStatusCode StatusCode { get; }

        public StagedPackage StagedPackage { get; }

        public string ErrorCode { get; }

        public string ErrorMessage { get; }

        public string ErrorTarget { get; }

        public IReadOnlyList<IValidationMessage> Warnings { get; }

        public bool Success => (int)StatusCode >= (int)HttpStatusCode.OK && (int)StatusCode < (int)HttpStatusCode.MultipleChoices;

        public static PackageStagingResult Created(StagedPackage stagedPackage, IReadOnlyList<IValidationMessage> warnings)
        {
            return new PackageStagingResult(HttpStatusCode.Created, stagedPackage, errorCode: null, errorMessage: null, errorTarget: null, warnings);
        }

        public static PackageStagingResult Ok(StagedPackage stagedPackage = null, IReadOnlyList<IValidationMessage> warnings = null)
        {
            return new PackageStagingResult(HttpStatusCode.OK, stagedPackage, errorCode: null, errorMessage: null, errorTarget: null, warnings);
        }

        public static PackageStagingResult Error(
            HttpStatusCode statusCode,
            string errorMessage,
            string errorCode = StagingApiErrorCodes.PackageUploadFailed,
            string errorTarget = null)
        {
            return new PackageStagingResult(statusCode, stagedPackage: null, errorCode, errorMessage, errorTarget, warnings: null);
        }
    }
}
