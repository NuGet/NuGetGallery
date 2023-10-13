// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Services.GitHub.GraphQL;
using Newtonsoft.Json;

namespace NuGet.Services.GitHub.Collector
{
    public class AdvisoryQueryBuilder : IAdvisoryQueryBuilder
    {
        private const string SecurityAdvisoryFields = @"databaseId
    ghsaId
    permalink
    severity
    withdrawnAt
    updatedAt";

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
        " + SecurityAdvisoryFields + @"
        " + CreateVulnerabilitiesConnectionQuery() + @"
      }
    }
  }
}";

        /// <summary>
        /// Source: https://docs.github.com/en/enterprise-cloud@latest/graphql/reference/queries#securityadvisory
        /// </summary>
        public string CreateSecurityAdvisoryQuery(SecurityAdvisory advisory)
            => @"
{
  securityAdvisory(ghsaId: " + JsonConvert.SerializeObject(advisory.GhsaId) + @") {
    " + SecurityAdvisoryFields + @"
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