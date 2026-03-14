// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Cursor;
using NuGet.Services.GitHub.Collector;
using NuGet.Services.GitHub.GraphQL;
using NuGet.Services.GitHub.Ingest;
using Xunit;

namespace GitHubVulnerabilities2v3.Facts
{
    public class AdvisoryCollectorFacts
    {
        private readonly Mock<ReadWriteCursor<DateTimeOffset>> _cursorMock;
        private readonly Mock<IAdvisoryQueryService> _queryServiceMock;
        private readonly List<IAdvisoryIngestor> _ingestors;
        private readonly Mock<IAdvisoryIngestor> _defaultIngestorMock;
        private readonly AdvisoryCollector _target;

        DateTimeOffset _cursorValue = DateTimeOffset.UtcNow.AddHours(-2);

        public AdvisoryCollectorFacts()
        {
            _cursorMock = new Mock<ReadWriteCursor<DateTimeOffset>>();
            _queryServiceMock = new Mock<IAdvisoryQueryService>();
            _ingestors = new List<IAdvisoryIngestor>();
            _defaultIngestorMock = new Mock<IAdvisoryIngestor>();

            _cursorMock
                .SetupGet(c => c.Value)
                .Returns(() => _cursorValue);

            _target = new AdvisoryCollector(
                _cursorMock.Object,
                _queryServiceMock.Object,
                _ingestors,
                Mock.Of<ILogger<AdvisoryCollector>>());

            _ingestors.Add(_defaultIngestorMock.Object);
        }

        [Fact]
        public async Task RequestsAdvisoriesSinceCursor()
        {
            await _target.ProcessAsync(CancellationToken.None, updateCursor: false);

            _cursorMock
                .VerifyGet(c => c.Value, Times.AtLeastOnce);
            _queryServiceMock
                .Verify(qs => qs.GetAdvisoriesSinceAsync(_cursorValue, It.IsAny<CancellationToken>()), Times.Once);
            _queryServiceMock
                .Verify(qs => qs.GetAdvisoriesSinceAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DoesntIngestEmptyAdvisoryList(bool useEmptyCollection)
        {
            _queryServiceMock
                .Setup(qs => qs.GetAdvisoriesSinceAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(useEmptyCollection ? Array.Empty<SecurityAdvisory>() : null);

            await _target.ProcessAsync(CancellationToken.None, updateCursor: false);

            _defaultIngestorMock
                .Verify(i => i.IngestAsync(It.IsAny<IReadOnlyList<SecurityAdvisory>>()), Times.Never);
        }

        [Fact]
        public async Task PassesAdvisoriesToAllIngestors()
        {
            var secondaryIngestor = new Mock<IAdvisoryIngestor>();
            _ingestors.Add(secondaryIngestor.Object);

            var latestAdvisoryTime = DateTimeOffset.UtcNow.AddSeconds(-42);

            SecurityAdvisory[] advisories = [
                new SecurityAdvisory { UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5) },
                new SecurityAdvisory { UpdatedAt = latestAdvisoryTime }
            ];

            _queryServiceMock
                .Setup(qs => qs.GetAdvisoriesSinceAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(advisories);

            await _target.ProcessAsync(CancellationToken.None, updateCursor: false);

            _defaultIngestorMock
                .Verify(i => i.IngestAsync(advisories), Times.Once);
            _defaultIngestorMock
                .Verify(i => i.IngestAsync(It.IsAny<IReadOnlyList<SecurityAdvisory>>()), Times.Once);
            secondaryIngestor
                .Verify(i => i.IngestAsync(advisories), Times.Once);
            secondaryIngestor
                .Verify(i => i.IngestAsync(It.IsAny<IReadOnlyList<SecurityAdvisory>>()), Times.Once);
        }
    }
}
