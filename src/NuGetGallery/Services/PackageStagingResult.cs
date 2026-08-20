// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;

namespace NuGetGallery
{
    public class PackageStagingResult
    {
        private PackageStagingResult(HttpStatusCode statusCode, string errorMessage, IReadOnlyList<IValidationMessage> warnings)
        {
            StatusCode = statusCode;
            ErrorMessage = errorMessage;
            Warnings = warnings ?? Array.Empty<IValidationMessage>();
        }

        public HttpStatusCode StatusCode { get; }

        public string ErrorMessage { get; }

        public IReadOnlyList<IValidationMessage> Warnings { get; }

        public bool Success => StatusCode == HttpStatusCode.Created;

        public static PackageStagingResult Created(IReadOnlyList<IValidationMessage> warnings)
        {
            return new PackageStagingResult(HttpStatusCode.Created, errorMessage: null, warnings);
        }

        public static PackageStagingResult Error(HttpStatusCode statusCode, string errorMessage)
        {
            return new PackageStagingResult(statusCode, errorMessage, warnings: null);
        }
    }
}
