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
            string snupkgUrl)
        {
            ValidationId = validationId;
            SymbolsPackageKey = symbolPackageKey;
            PackageId = packageId;
            PackageNormalizedVersion = packageNormalizedVersion;
            SnupkgUrl = snupkgUrl;
        }

        public Guid ValidationId { get; }

        public int SymbolsPackageKey { get; }

        public string PackageId { get; }

        public string PackageNormalizedVersion { get; }

        public string SnupkgUrl { get; }
    }
}
