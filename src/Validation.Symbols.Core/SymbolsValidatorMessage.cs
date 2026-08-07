// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Validation.Symbols.Core
{
    public class SymbolsValidatorMessage : ISymbolsValidatorMessage
    {
        public SymbolsValidatorMessage(Guid validationId, 
            int symbolPackageKey,
            string packageId,
            string packageNormalizedVersion,
            string snupkgUrl,
            string parentNupkgSnapshotUrl = null)
        {
            ValidationId = validationId;
            SymbolsPackageKey = symbolPackageKey;
            PackageId = packageId;
            PackageNormalizedVersion = packageNormalizedVersion;
            SnupkgUrl = snupkgUrl;
            ParentNupkgSnapshotUrl = parentNupkgSnapshotUrl;
        }

        public Guid ValidationId { get; }

        public int SymbolsPackageKey { get; }

        public string PackageId { get; }

        public string PackageNormalizedVersion { get; }

        public string SnupkgUrl { get; }

        /// <summary>
        /// The URL of the immutable copy of the exact parent nupkg bytes to which this symbol validation attempt is
        /// bound. Null for ordinary symbol validation, which resolves the parent by package ID and version.
        /// </summary>
        public string ParentNupkgSnapshotUrl { get; }
    }
}
