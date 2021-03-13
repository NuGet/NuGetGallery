// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using NuGetGallery;

namespace VerifyGitHubVulnerabilities.Verify
{
    /// <summary>
    /// We need a <see cref=IPackageVulnerabilitiesManagementService" /> for inserting the verifier into the ingestion job,
    /// so we extend for our extra reporting data.
    /// </summary>
    public interface IPackageVulnerabilitiesVerifier : IPackageVulnerabilitiesManagementService
    {
        /// <summary>
        /// An error flag for verification
        /// </summary>
        bool HasErrors { get; }
    }
}