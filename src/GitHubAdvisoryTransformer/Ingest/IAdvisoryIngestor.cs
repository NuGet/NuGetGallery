// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using GitHubAdvisoryTransformer.GraphQL;

namespace GitHubAdvisoryTransformer.Ingest
{
    /// <summary>
    /// Processes new or updated <see cref="SecurityAdvisory"/>s.
    /// </summary>
    public interface IAdvisoryIngestor
    {
        Task IngestAsync(IReadOnlyList<SecurityAdvisory> advisories);
    }
}