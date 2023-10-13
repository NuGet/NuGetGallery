// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.GitHub.Configuration;

namespace GitHubVulnerabilities2Db.Configuration
{
    public class GitHubVulnerabilities2DbConfiguration : GraphQLQueryConfiguration
    {
        /// <summary>
        /// The storage connection to use to save the job's cursor.
        /// </summary>
        public string StorageConnectionString { get; set; }

        /// <summary>
        /// The storage container to save the job's cursor in.
        /// </summary>
        public string CursorContainerName { get; set; } = "vulnerability";

        /// <summary>
        /// The name of the blob to save the job's advisories cursor in.
        /// </summary>
        public string AdvisoryCursorBlobName { get; set; } = "cursor.json";
    }
}