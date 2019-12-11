// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace GitHubVulnerabilities2Db.GraphQL
{
    /// <summary>
    /// https://developer.github.com/v4/object/securityadvisory/
    /// </summary>
    public class SecurityAdvisory : INode
    {
        public int DatabaseId { get; set; }

        /// <summary>
        /// The GitHub SecurityAdvisory ID in the format 'GHSA-xxxx-xxxx-xxxx'.
        /// </summary>
        public string GhsaId { get; set; }

        public string Severity { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset? WithdrawnAt { get; set; }
        public ConnectionResponseData<SecurityVulnerability> Vulnerabilities { get; set; }
    }
}