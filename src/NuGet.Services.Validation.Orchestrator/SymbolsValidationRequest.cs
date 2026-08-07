// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Validation.Orchestrator
{
    public sealed class SymbolsValidationRequest : NuGetValidationRequest
    {
        public SymbolsValidationRequest(
            Guid validationId,
            int packageKey,
            string packageId,
            string packageVersion,
            string snupkgUrl,
            string parentNupkgSnapshotUrl = null)
            : base(validationId, packageKey, packageId, packageVersion, snupkgUrl)
        {
            ParentNupkgSnapshotUrl = parentNupkgSnapshotUrl;
        }

        public string ParentNupkgSnapshotUrl { get; }
    }
}
