// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace GitHubVulnerabilities2Db.Ingest
{
    /// <summary>
    /// An exception thrown by <see cref="GitHubVersionRangeParser"/> when the string provided to <see cref="GitHubVersionRangeParser.ToNuGetVersionRange(string)"/> is malformed.
    /// </summary>
    public class GitHubVersionRangeParsingException : ArgumentException
    {
        public GitHubVersionRangeParsingException(string invalidVersionRange, string message)
            : base(message)
        {
            InvalidVersionRange = invalidVersionRange;
        }

        public string InvalidVersionRange { get; set; }
    }
}
