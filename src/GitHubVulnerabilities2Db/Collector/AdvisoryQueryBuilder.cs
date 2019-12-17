// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using GitHubVulnerabilities2Db.GraphQL;

namespace GitHubVulnerabilities2Db.Collector
{
    public class AdvisoryQueryBuilder : IAdvisoryQueryBuilder
    {
        public int GetMaximumResultsPerRequest() => 100;

        public string CreateSecurityAdvisoriesQuery(DateTimeOffset? updatedSince = null, string afterCursor = null)
            => @"
{
  securityAdvisories(first: " + GetMaximumResultsPerRequest() + ", orderBy: {field: UPDATED_AT, direction: ASC}" +
            (!updatedSince.HasValue || updatedSince == DateTimeOffset.MinValue ? "" : $", updatedSince: \"{updatedSince.Value.ToString("O")}\"") +
            (string.IsNullOrWhiteSpace(afterCursor) ? "" : $", after: \"{afterCursor}\"") + @") {
    edges {
      cursor
      node {
        databaseId
        ghsaId
        severity
        updatedAt
        " + CreateVulnerabilitiesConnectionQuery() + @"
      }
    }
  }
}";

        public string CreateSecurityAdvisoryQuery(SecurityAdvisory advisory)
            => @"
{
  securityAdvisory(ghsaId: " + advisory.GhsaId + @") {
    severity
    updatedAt
    identifiers {
      type
      value
    }
    " + CreateVulnerabilitiesConnectionQuery(advisory.Vulnerabilities?.Edges?.Last()?.Cursor) + @"
  }
}";

        private string CreateVulnerabilitiesConnectionQuery(string edgeCursor = null)
            => @"vulnerabilities(first: " + GetMaximumResultsPerRequest() + @", ecosystem: NUGET, orderBy: {field: UPDATED_AT, direction: ASC}" + (string.IsNullOrEmpty(edgeCursor) ? "" : $", after: \"{edgeCursor}\"") + @") {
        edges {
          cursor
          node {
            package {
              name
            }
            firstPatchedVersion {
                identifier
            }
            vulnerableVersionRange
            updatedAt
          }
        }
      }";
    }
}