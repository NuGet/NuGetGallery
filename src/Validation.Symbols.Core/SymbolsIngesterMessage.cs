// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Validation.Symbols.Core
{
    public class SymbolsIngesterMessage : ISymbolsValidatorMessage
    {
        public SymbolsIngesterMessage(Guid validationId,
            int symbolPackageKey,
            string packageId,
            string packageNormalizedVersion,
            string snupkgUrl,
            string requestName)
        {
            ValidationId = validationId;
            SymbolsPackageKey = symbolPackageKey;
            PackageId = packageId;
            PackageNormalizedVersion = packageNormalizedVersion;
            SnupkgUrl = snupkgUrl;
            RequestName = requestName;
        }

        public Guid ValidationId { get; }

        public int SymbolsPackageKey { get; }

        public string PackageId { get; }

        public string PackageNormalizedVersion { get; }

        public string SnupkgUrl { get; }

        /// <summary>
        /// This is the request name to be used when ingesting a symbols package to VSTS.
        /// </summary>
        public string RequestName { get; }
    }
}
