// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GitHubVulnerabilities2Db.Ingest;
using NuGet.Services.Cursor;

namespace GitHubVulnerabilities2Db.Collector
{
    public class AdvisoryCollector : IAdvisoryCollector
    {
        public AdvisoryCollector(
            ReadWriteCursor<DateTimeOffset> cursor,
            IAdvisoryCollectorQueryService queryService,
            IAdvisoryIngestor ingestor)
        {
            _cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
            _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
            _ingestor = ingestor ?? throw new ArgumentNullException(nameof(ingestor));
        }

        private readonly ReadWriteCursor<DateTimeOffset> _cursor;
        private readonly IAdvisoryCollectorQueryService _queryService;
        private readonly IAdvisoryIngestor _ingestor;

        public async Task<bool> Process(CancellationToken token)
        {
            var advisories = await _queryService.GetAdvisoriesSinceAsync(_cursor, token);
            var hasAdvisories = advisories != null && advisories.Any();
            if (hasAdvisories)
            {
                var lastUpdatedAt = advisories.Max(i => i.UpdatedAt);
                await _ingestor.Ingest(advisories.Select(v => v).ToList());
                _cursor.Value = lastUpdatedAt;
                await _cursor.Save(token);
            }

            return hasAdvisories;
        }
    }
}