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
            string parentPackageUrl = null)
        {
            ValidationId = validationId;
            SymbolsPackageKey = symbolPackageKey;
            PackageId = packageId;
            PackageNormalizedVersion = packageNormalizedVersion;
            SnupkgUrl = snupkgUrl;
            ParentPackageUrl = parentPackageUrl;
        }

        public Guid ValidationId { get; }

        public int SymbolsPackageKey { get; }

        public string PackageId { get; }

        public string PackageNormalizedVersion { get; }

        public string SnupkgUrl { get; }

        /// <summary>
        /// Gets the private staged parent URL, or null to use ordinary package storage.
        /// </summary>
        public string ParentPackageUrl { get; }
    }
}
