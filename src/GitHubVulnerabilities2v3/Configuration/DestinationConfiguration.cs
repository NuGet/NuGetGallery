// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace GitHubVulnerabilities2v3.Configuration
{
    public class DestinationConfiguration
    {
        /// <summary>
        /// The storage connection to use to save the job's output.
        /// </summary>
        public string StorageConnectionString { get; set; }

        /// <summary>
        /// Base URL to use for absolute URLs of child documents.
        /// </summary>
        public string V3BaseUrl { get; set; }
    }
}
