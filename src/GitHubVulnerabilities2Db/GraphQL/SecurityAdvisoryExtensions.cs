// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace GitHubVulnerabilities2Db.GraphQL
{
    public static class SecurityAdvisoryExtensions
    {
        /// <summary>
        /// Gets the permalink for the security advisory.
        /// </summary>
        /// <param name="advisory"></param>
        public static string GetPermalink(this SecurityAdvisory advisory)
        {
            if (advisory == null)
            {
                throw new ArgumentNullException(nameof(advisory));
            }

            if (string.IsNullOrEmpty(advisory.GhsaId))
            {
                throw new ArgumentException("Cannot create a permalink for security advisory without GHSA ID.", nameof(advisory.GhsaId));
            }

            // This is the permalink format used by GitHub for security advisory pages.
            // Note that the GHSA ID part of the URL is case-sensitive, so we pass in the GHSA ID as-is.

            // Todo: remove this hard-coded work-around when the "permalink" field becomes available on the GH API.
            // See: https://developer.github.com/v4/object/securityadvisory/

            return $"https://github.com/advisories/{advisory.GhsaId}";
        }
    }
}