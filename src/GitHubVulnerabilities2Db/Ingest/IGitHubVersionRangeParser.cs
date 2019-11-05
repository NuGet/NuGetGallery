// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using GitHubVulnerabilities2Db.GraphQL;
using NuGet.Versioning;

namespace GitHubVulnerabilities2Db.Ingest
{
    /// <summary>
    /// Parses <see cref="SecurityVulnerability.VulnerableVersionRange"/> into a <see cref="VersionRange"/>.
    /// </summary>
    public interface IGitHubVersionRangeParser
    {
        VersionRange ToNuGetVersionRange(string gitHubVersionRange);
    }
}