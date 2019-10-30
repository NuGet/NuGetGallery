// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GitHubVulnerabilities2Db.GraphQL;
using NuGet.Services.Cursor;

namespace GitHubVulnerabilities2Db.Collector
{
    /// <summary>
    /// Wrapper around <see cref="IQueryService"/> to make it easier to query for <see cref="SecurityAdvisory"/>s using a cursor.
    /// </summary>
    public interface IAdvisoryQueryService
    {
        Task<IReadOnlyList<SecurityAdvisory>> GetAdvisoriesSinceAsync(ReadCursor<DateTimeOffset> cursor, CancellationToken token);
    }
}