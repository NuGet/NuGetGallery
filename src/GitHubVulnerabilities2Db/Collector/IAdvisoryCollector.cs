// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace GitHubVulnerabilities2Db.Collector
{
    public interface IAdvisoryCollector
    {
        /// <summary>
        /// Queries for any new or updated advisories using a cursor, processes them, and then updates the cursor.
        /// </summary>
        /// <remarks>Whether or not any advisories were processed.</remarks>
        Task<bool> ProcessAsync(CancellationToken token);
    }
}