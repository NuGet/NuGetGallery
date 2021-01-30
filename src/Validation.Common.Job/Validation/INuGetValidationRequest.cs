// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Validation
{
    /// <summary>
    /// The request to start or check a validation step for a NuGet package or symbol.
    /// </summary>
    public interface INuGetValidationRequest
    {
        /// <summary>
        /// The identifier for a single validation step execution.
        /// </summary>
        Guid ValidationId { get; }

        /// <summary>
        /// The package key in the NuGet gallery database. If a package is hard deleted and created, the package key
        /// will be different but the <see cref="PackageId"/> and <see cref="PackageVersion"/> will be the same.
        /// </summary>
        int PackageKey { get; }

        /// <summary>
        /// The package ID. The casing of this ID need not match the author-intended casing of the ID.
        /// </summary>
        string PackageId { get; }

        /// <summary>
        /// The package version. The casing of this version need not match the author-intended casing of the version.
        /// This value is not necessarily a normalized version.
        /// </summary>
        string PackageVersion { get; }

        /// <summary>
        /// The URL to the NuGet package content. This URL should be accessible without special authentication headers.
        /// However, authentication information could be included in the URL (e.g. Azure Blob Storage SAS URL). This URL
        /// need not have a single value for a specific <see cref="ValidationId"/>.
        /// </summary>
        string NupkgUrl { get; }
    }
}
