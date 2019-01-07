// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// The exception thrown when a catalog entry is missing a package signature file.
    /// This indicates that the package is not signed.
    /// See: https://github.com/NuGet/Home/wiki/Package-Signatures-Technical-Details#-the-package-signature-file
    /// </summary>
    public sealed class MissingPackageSignatureFileException : ValidationException
    {
        public MissingPackageSignatureFileException(Uri catalogEntry, string message)
            : base(message)
        {
            CatalogEntry = catalogEntry;

            Data.Add(nameof(CatalogEntry), catalogEntry);
        }

        public Uri CatalogEntry { get; }
    }
}
