// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GitHubVulnerabilities2Db.Ingest;
using Microsoft.Extensions.Logging;
using NuGet.Services.Cursor;

namespace GitHubVulnerabilities2Db.Collector
{
    public class AdvisoryCollector : IAdvisoryCollector
    {
        private readonly ReadWriteCursor<DateTimeOffset> _cursor;
        private readonly IAdvisoryQueryService _queryService;
        private readonly IAdvisoryIngestor _ingestor;
        private readonly ILogger<AdvisoryCollector> _logger;

        public AdvisoryCollector(
            ReadWriteCursor<DateTimeOffset> cursor,
            IAdvisoryQueryService queryService,
            IAdvisoryIngestor ingestor,
            ILogger<AdvisoryCollector> logger)
        {
            _cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
            _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
            _ingestor = ingestor ?? throw new ArgumentNullException(nameof(ingestor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> ProcessAsync(CancellationToken token)
        {
            var advisories = await _queryService.GetAdvisoriesSinceAsync(_cursor, token);
            var hasAdvisories = advisories != null && advisories.Any();
            _logger.LogInformation("Found {AdvisoryCount} new advisories to process", advisories?.Count() ?? 0);
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