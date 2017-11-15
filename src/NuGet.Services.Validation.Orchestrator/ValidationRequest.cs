// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ValidationRequest : IValidationRequest
    {
        public Guid ValidationId { get; }

        public int PackageKey { get; }

        public string PackageId { get; }

        public string PackageVersion { get; }

        public string NupkgUrl { get; }

        public ValidationRequest(Guid validationId, int packageKey, string packageId, string packageVersion, string nupkgUrl)
        {
            ValidationId = validationId;
            PackageKey = packageKey;
            PackageId = packageId;
            PackageVersion = packageVersion;
            NupkgUrl = nupkgUrl;
        }
    }
}
