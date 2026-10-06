// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Describes an artifact upload outcome and its accepted staging attempt.
    /// </summary>
    public class PackageStagingResult
    {
        private PackageStagingResult(HttpStatusCode statusCode, string errorMessage, IReadOnlyList<IValidationMessage> warnings, StagedPackage stagedPackage = null, StagedSymbolPackage stagedSymbolPackage = null)
        {
            StatusCode = statusCode;
            ErrorMessage = errorMessage;
            Warnings = warnings ?? Array.Empty<IValidationMessage>();
            StagedPackage = stagedPackage;
            StagedSymbolPackage = stagedSymbolPackage;
        }

        public HttpStatusCode StatusCode { get; }

        public string ErrorMessage { get; }

        public IReadOnlyList<IValidationMessage> Warnings { get; }

        /// <summary>
        /// Gets the accepted package attempt for a successful package upload.
        /// </summary>
        public StagedPackage StagedPackage { get; }

        /// <summary>
        /// Gets the accepted symbol attempt for a successful symbol upload.
        /// </summary>
        public StagedSymbolPackage StagedSymbolPackage { get; }

        public bool Success => (int)StatusCode >= (int)HttpStatusCode.OK && (int)StatusCode < (int)HttpStatusCode.MultipleChoices;

        public static PackageStagingResult Created(IReadOnlyList<IValidationMessage> warnings, StagedPackage stagedPackage = null, StagedSymbolPackage stagedSymbolPackage = null)
        {
            return new PackageStagingResult(HttpStatusCode.Created, errorMessage: null, warnings, stagedPackage, stagedSymbolPackage);
        }

        public static PackageStagingResult Ok(IReadOnlyList<IValidationMessage> warnings = null, StagedPackage stagedPackage = null, StagedSymbolPackage stagedSymbolPackage = null)
        {
            return new PackageStagingResult(HttpStatusCode.OK, errorMessage: null, warnings, stagedPackage, stagedSymbolPackage);
        }

        public static PackageStagingResult Error(HttpStatusCode statusCode, string errorMessage)
        {
            return new PackageStagingResult(statusCode, errorMessage, warnings: null);
        }
    }
}
