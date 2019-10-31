// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using GitHubVulnerabilities2Db.Collector;
using GitHubVulnerabilities2Db.GraphQL;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Cursor;
using Xunit;

namespace GitHubVulnerabilities2Db.Facts
{
    public class AdvisoryQueryServiceFacts
    {
        public AdvisoryQueryServiceFacts()
        {
            _token = CancellationToken.None;
            _cursorMock = new Mock<ReadWriteCursor<DateTimeOffset>>();
            _cursorMock
                .Setup(x => x.Load(_token))
                .Returns(Task.CompletedTask)
                .Verifiable();

            _cursorValue = new DateTimeOffset(2019, 10, 28, 1, 1, 1, TimeSpan.Zero);
            _cursorMock
                .Setup(x => x.Value)
                .Returns(_cursorValue);

            _queryServiceMock = new Mock<IQueryService>();
            _maxResultsPerQuery = 3;
            _queryBuilderMock = new Mock<IAdvisoryQueryBuilder>();
            _queryBuilderMock
                .Setup(x => x.GetMaximumResultsPerRequest())
                .Returns(_maxResultsPerQuery);

            _service = new AdvisoryQueryService(
                _queryServiceMock.Object,
                _queryBuilderMock.Object,
                Mock.Of<ILogger<AdvisoryQueryService>>());
        }

        private readonly Mock<IQueryService> _queryServiceMock;
        private readonly int _maxResultsPerQuery;
        private readonly Mock<IAdvisoryQueryBuilder> _queryBuilderMock;
        private readonly Mock<ReadWriteCursor<DateTimeOffset>> _cursorMock;
        private readonly DateTimeOffset _cursorValue;
        private readonly CancellationToken _token;
        private readonly AdvisoryQueryService _service;

        [Fact]
        public async Task NoResults()
        {
            // Arrange
            SetupFirstQueryResult(new QueryResponse());

            // Act
            var results = await _service.GetAdvisoriesSinceAsync(
                _cursorMock.Object, _token);

            // Assert
            Assert.Empty(results);

            _cursorMock.Verify();
            _queryBuilderMock.Verify();
            _queryServiceMock.Verify();
        }

        [Fact]
        public async Task OneResult()
        {
            // Arrange
            var vulnerability = new SecurityVulnerability();
            var advisory = new SecurityAdvisory
            {
                Vulnerabilities = new ConnectionResponseData<SecurityVulnerability>
                {
                    Edges = new[] { new Edge<SecurityVulnerability> { Node = vulnerability } }
                }
            };

            var response = CreateResponseFromEdges(new[] { new Edge<SecurityAdvisory> { Node = advisory } });
            SetupFirstQueryResult(response);

            // Act
            var results = await _service.GetAdvisoriesSinceAsync(
                _cursorMock.Object, _token);

            // Assert
            Assert.Single(results, advisory);

            _cursorMock.Verify();
            _queryBuilderMock.Verify();
            _queryServiceMock.Verify();
        }

        [Fact]
        public async Task DedupesIdenticalVulnerabilities()
        {
            // Arrange
            var id = "identical";
            var range = "(,)";
            var firstVulnerability = new SecurityVulnerability
            {
                Package = new SecurityVulnerabilityPackage { Name = id },
                VulnerableVersionRange = range
            };

            var secondVulnerability = new SecurityVulnerability
            {
                Package = new SecurityVulnerabilityPackage { Name = id },
                VulnerableVersionRange = range
            };

            var advisory = new SecurityAdvisory
            {
                Vulnerabilities = new ConnectionResponseData<SecurityVulnerability>
                {
                    Edges = new[] 
                    {
                        new Edge<SecurityVulnerability> { Node = firstVulnerability },
                        new Edge<SecurityVulnerability> { Node = secondVulnerability }
                    }
                }
            };

            var response = CreateResponseFromEdges(new[] { new Edge<SecurityAdvisory> { Node = advisory } });
            SetupFirstQueryResult(response);

            // Act
            var results = await _service.GetAdvisoriesSinceAsync(
                _cursorMock.Object, _token);

            // Assert
            Assert.Single(results, advisory);
            Assert.Single(results.Single().Vulnerabilities.Edges);
            var node = results.Single().Vulnerabilities.Edges.Single().Node;
            Assert.Equal(id, node.Package.Name);
            Assert.Equal(range, node.VulnerableVersionRange);

            _cursorMock.Verify();
            _queryBuilderMock.Verify();
            _queryServiceMock.Verify();
        }

        [Fact]
        public async Task ManyResultsShouldPage()
        {
            // Arrange
            var firstEdges = Enumerable
                .Range(0, _maxResultsPerQuery)
                .Select(i => new Edge<SecurityAdvisory> { Cursor = i.ToString(), Node = new SecurityAdvisory { DatabaseId = i } });

            var firstResponse = CreateResponseFromEdges(firstEdges);
            SetupFirstQueryResult(firstResponse);

            var secondEdges = Enumerable
                .Range(_maxResultsPerQuery, _maxResultsPerQuery)
                .Select(i => new Edge<SecurityAdvisory> { Cursor = i.ToString(), Node = new SecurityAdvisory { DatabaseId = i } });

            var secondResponse = CreateResponseFromEdges(secondEdges);
            SetupAfterCursorQueryResponse(firstEdges.Last().Cursor, secondResponse);

            var thirdResponse = CreateResponseFromEdges(Enumerable.Empty<Edge<SecurityAdvisory>>());
            SetupAfterCursorQueryResponse(secondEdges.Last().Cursor, thirdResponse);

            // Act
            var results = await _service.GetAdvisoriesSinceAsync(
                _cursorMock.Object, _token);

            // Assert
            Assert.Equal(_maxResultsPerQuery * 2, results.Count());
            Assert.Equal(_maxResultsPerQuery * 2 - 1, results.Last().DatabaseId);

            _cursorMock.Verify();
            _queryBuilderMock.Verify();
            _queryServiceMock.Verify();
        }

        [Fact]
        public async Task OneResultWithManyVulnerabilitiesShouldPage()
        {
            // Arrange
            var firstVulnerabilityEdges = Enumerable
                .Range(0, _maxResultsPerQuery)
                .Select(i => new Edge<SecurityVulnerability>
                {
                    Cursor = i.ToString(),
                    Node = new SecurityVulnerability
                    {
                        VulnerableVersionRange = i.ToString()
                    }
                });

            var firstAdvisoryEdge = new Edge<SecurityAdvisory>
            {
                Node = new SecurityAdvisory
                {
                    Vulnerabilities = new ConnectionResponseData<SecurityVulnerability>
                    {
                        Edges = firstVulnerabilityEdges
                    }
                }
            };

            var firstResponse = CreateResponseFromEdges(new[] { firstAdvisoryEdge });
            SetupFirstQueryResult(firstResponse);

            var secondVulnerabilityEdges = Enumerable
                .Range(_maxResultsPerQuery, _maxResultsPerQuery)
                .Select(i => new Edge<SecurityVulnerability>
                {
                    Cursor = i.ToString(),
                    Node = new SecurityVulnerability
                    {
                        VulnerableVersionRange = i.ToString()
                    }
                });

            var secondAdvisory = new SecurityAdvisory
            {
                Vulnerabilities = new ConnectionResponseData<SecurityVulnerability>
                {
                    Edges = secondVulnerabilityEdges
                }
            };

            var secondResponse = CreateResponseFromAdvisory(secondAdvisory);
            SetupAdditionalVulnerabilitiesQueryResponse((_maxResultsPerQuery - 1).ToString(), secondResponse);

            var thirdAdvisory = new SecurityAdvisory
            {
                Vulnerabilities = new ConnectionResponseData<SecurityVulnerability>
                {
                    Edges = Enumerable.Empty<Edge<SecurityVulnerability>>()
                }
            };

            var thirdResponse = CreateResponseFromAdvisory(thirdAdvisory);
            SetupAdditionalVulnerabilitiesQueryResponse((_maxResultsPerQuery * 2 - 1).ToString(), thirdResponse);

            // Act
            var results = await _service.GetAdvisoriesSinceAsync(
                _cursorMock.Object, _token);

            // Assert
            Assert.Equal(_maxResultsPerQuery * 2, results.Single().Vulnerabilities.Edges.Count());
            Assert.Equal((_maxResultsPerQuery * 2 - 1).ToString(), results.Single().Vulnerabilities.Edges.Last().Cursor);

            _cursorMock.Verify();
            _queryBuilderMock.Verify();
            _queryServiceMock.Verify();
        }

        private QueryResponse CreateResponseFromEdges(IEnumerable<Edge<SecurityAdvisory>> edges)
            => new QueryResponse
            {
                Data = new QueryResponseData
                {
                    SecurityAdvisories = new ConnectionResponseData<SecurityAdvisory>
                    {
                        Edges = edges
                    }
                }
            };

        private QueryResponse CreateResponseFromAdvisory(SecurityAdvisory advisory)
            => new QueryResponse
            {
                Data = new QueryResponseData
                {
                    SecurityAdvisory = advisory
                }
            };

        private void SetupFirstQueryResult(QueryResponse response)
            => SetupQueryResult(
                x => x.CreateSecurityAdvisoriesQuery(_cursorValue, null),
                response);

        private void SetupAfterCursorQueryResponse(string afterCursor, QueryResponse response)
            => SetupQueryResult(
                x => x.CreateSecurityAdvisoriesQuery(null, afterCursor),
                response);

        private void SetupAdditionalVulnerabilitiesQueryResponse(string lastCursor, QueryResponse response)
            => SetupQueryResult(
                x => x.CreateSecurityAdvisoryQuery(
                    It.Is<SecurityAdvisory>(
                        a => lastCursor == null || a.Vulnerabilities.Edges.Last().Cursor == lastCursor)),
                response);

        private void SetupQueryResult(
            Expression<Func<IAdvisoryQueryBuilder, string>> expression,
            QueryResponse response)
        {
            var query = Guid.NewGuid().ToString();
            _queryBuilderMock
                .Setup(expression)
                .Returns(query)
                .Verifiable();

            _queryServiceMock
                .Setup(x => x.QueryAsync(query, _token))
                .ReturnsAsync(response)
                .Verifiable();
        }
    }
}