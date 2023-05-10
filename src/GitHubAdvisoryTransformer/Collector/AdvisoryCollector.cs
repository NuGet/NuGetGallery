// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GitHubAdvisoryTransformer.Ingest;
using GitHubAdvisoryTransformer.Cursor;

namespace GitHubAdvisoryTransformer.Collector
{
    public class AdvisoryCollector : IAdvisoryCollector
    {
        private readonly ReadWriteCursor<DateTimeOffset> _cursor;
        private readonly IAdvisoryQueryService _queryService;
        private readonly IAdvisoryIngestor _ingestor;

        public AdvisoryCollector(
            ReadWriteCursor<DateTimeOffset> cursor,
            IAdvisoryQueryService queryService,
            IAdvisoryIngestor ingestor)
        {
            _cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
            _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
            _ingestor = ingestor ?? throw new ArgumentNullException(nameof(ingestor));
        }

        public async Task<bool> ProcessAsync(CancellationToken token)
        {
            await _cursor.Load(token);
            var lastUpdated = _cursor.Value;
            var advisories = await _queryService.GetAdvisoriesSinceAsync(lastUpdated, token);
            var hasAdvisories = advisories != null && advisories.Any();
            Console.WriteLine($"Found {advisories?.Count() ?? 0} new advisories to process");
            if (hasAdvisories)
            {
                var lastUpdatedAt = advisories.Max(i => i.UpdatedAt);
                await _ingestor.IngestAsync(advisories.Select(v => v).ToList());
                _cursor.Value = lastUpdatedAt;
                await _cursor.Save(token);
            }

            return hasAdvisories;
        }
    }
}