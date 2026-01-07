// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Moq;
using NuGet.Jobs.Validation;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGet.Services.Validation.Symbols;
using NuGet.Jobs.Validation.Symbols.Core;

using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.Validation.Orchestrator.Tests.Symbol
{
    public class SymbolsIngesterFacts
    {
        private const int PackageKey = 1001;
        private const string PackageId = "NuGet.Versioning";
        private const string PackageVersion = "1.2.3";
        private static readonly Guid ValidationId = new Guid("12345678-1234-1234-1234-123456789012");
        private const string NupkgUrl = "https://example/nuget.versioning/1.2.3/package.nupkg";
        private const string SnupkgUrl = "https://example/nuget.versioning/1.2.3/package.snupkg";

        public class TheGetResponseAsyncMethod : FactsBase
        {
            private static readonly ValidationStatus[] possibleValidationStatuses = new ValidationStatus[]
            {
                ValidationStatus.Incomplete,
                ValidationStatus.Failed,
                ValidationStatus.Succeeded
            };

            public TheGetResponseAsyncMethod(ITestOutputHelper output) : base(output)
            {
            }

            [Theory]
            [MemberData(nameof(PossibleValidationStatuses))]
            public async Task ReturnsPersistedStatus(ValidationStatus status)
            {
                // Arrange
                _symbolsValidationEntitiesService
                    .Setup(x => x.GetSymbolsServerRequestAsync(It.IsAny<INuGetValidationRequest>()))
                    .ReturnsAsync(new SymbolsServerRequest
                    {
                        Created = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow,
                        RequestName = PackageKey.ToString(),
                        RequestStatusKey = ConvertToSymbolsPackageIngestRequestStatus(status),
                        SymbolsKey = PackageKey
                    });

                // Act & Assert
                var actual = await _target.GetResponseAsync(_validationRequest.Object);

                Assert.Equal(status, actual.Status);
            }

            public static IEnumerable<object[]> PossibleValidationStatuses => possibleValidationStatuses.Select(s => new object[] { s });
        }

        public class TheStartValidationAsyncMethod : FactsBase
        {
            private static readonly ValidationStatus[] startedValidationStatuses = new ValidationStatus[]
            {
                ValidationStatus.Incomplete,
                ValidationStatus.Succeeded,
                ValidationStatus.Failed
            };

            public TheStartValidationAsyncMethod(ITestOutputHelper output) : base(output)
            {
            }

            [Theory]
            [MemberData(nameof(StartedValidationStatuses))]
            public async Task ReturnsPersistedStatusesIfValidationAlreadyStarted(ValidationStatus status)
            {
                // Arrange
                _symbolsValidationEntitiesService
                    .Setup(x => x.GetSymbolsServerRequestAsync(It.IsAny<INuGetValidationRequest>()))
                    .ReturnsAsync(new SymbolsServerRequest
                    {
                        Created = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow,
                        RequestName = PackageKey.ToString(),
                        RequestStatusKey = ConvertToSymbolsPackageIngestRequestStatus(status),
                        SymbolsKey = PackageKey
                    });

                // Act & Assert
                await _target.StartAsync(_validationRequest.Object);

                _symbolMessageEnqueuer
                    .Verify(x => x.EnqueueSymbolsIngestionMessageAsync(It.IsAny<INuGetValidationRequest>()), Times.Never);

                _symbolsValidationEntitiesService
                    .Verify(x => x.AddSymbolsServerRequestAsync(It.IsAny<SymbolsServerRequest>()), Times.Never);

                _telemetryService.Verify(
                    x => x.TrackSymbolsMessageEnqueued(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>()),
                    Times.Never);
            }

            [Fact]
            public async Task StartsValidationIfNotStarted()
            {
                // Arrange
                // The order of operations is important! The state MUST be persisted AFTER verification has been queued.
                var statePersisted = false;
                bool verificationQueuedBeforeStatePersisted = false;
                var ingestingRequest = new SymbolsServerRequest
                {
                    Created = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow,
                    RequestName = PackageKey.ToString(),
                    RequestStatusKey = SymbolsPackageIngestRequestStatus.Ingesting,
                    SymbolsKey = PackageKey
                };
                var symbolsIngesterMessage = new SymbolsIngesterMessage(ValidationId, PackageKey, PackageId, PackageVersion, SnupkgUrl, "DummyRequestName");

                _symbolsValidationEntitiesService
                     .Setup(x => x.GetSymbolsServerRequestAsync(It.IsAny<INuGetValidationRequest>()))
                     .ReturnsAsync((SymbolsServerRequest)null);

                _symbolMessageEnqueuer
                    .Setup(x => x.EnqueueSymbolsIngestionMessageAsync(It.IsAny<INuGetValidationRequest>()))
                    .Callback(() =>
                    {
                        verificationQueuedBeforeStatePersisted = !statePersisted;
                    })
                    .Returns(Task.FromResult(symbolsIngesterMessage));

                _symbolsValidationEntitiesService
                    .Setup(x => x.AddSymbolsServerRequestAsync(It.IsAny<SymbolsServerRequest>()))
                    .Callback(() =>
                    {
                        statePersisted = true;
                    })
                    .ReturnsAsync(ingestingRequest);

                // Act
                await _target.StartAsync(_validationRequest.Object);

                // Assert
                _symbolMessageEnqueuer
                    .Verify(x => x.EnqueueSymbolsIngestionMessageAsync(It.IsAny<INuGetValidationRequest>()), Times.Once);

                _symbolsValidationEntitiesService
                    .Verify(
                        x => x.AddSymbolsServerRequestAsync(It.IsAny<SymbolsServerRequest>()),
                        Times.Once);

                _telemetryService.Verify(
                    x => x.TrackSymbolsMessageEnqueued(_validationRequest.Object.PackageId, _validationRequest.Object.PackageVersion, ValidatorName.SymbolsIngester, _validationRequest.Object.ValidationId),
                    Times.Once);

                Assert.True(verificationQueuedBeforeStatePersisted);
            }

            public static IEnumerable<object[]> StartedValidationStatuses => startedValidationStatuses.Select(s => new object[] { s });
        }

        public abstract class FactsBase
        {
            protected readonly Mock<ISymbolsValidationEntitiesService> _symbolsValidationEntitiesService;
            protected readonly Mock<ISymbolsIngesterMessageEnqueuer> _symbolMessageEnqueuer;
            protected readonly Mock<ITelemetryService> _telemetryService;
            protected readonly ILogger<SymbolsIngester> _logger;
            protected readonly Mock<INuGetValidationRequest> _validationRequest;
            protected readonly SymbolsIngester _target;

            public FactsBase(ITestOutputHelper output)
            {
                _symbolsValidationEntitiesService = new Mock<ISymbolsValidationEntitiesService>();
                _symbolMessageEnqueuer = new Mock<ISymbolsIngesterMessageEnqueuer>();
                _telemetryService = new Mock<ITelemetryService>();
                var loggerFactory = new LoggerFactory().AddXunit(output);
                _logger = loggerFactory.CreateLogger<SymbolsIngester>();

                _validationRequest = new Mock<INuGetValidationRequest>();
                _validationRequest.Setup(x => x.NupkgUrl).Returns(NupkgUrl);
                _validationRequest.Setup(x => x.PackageId).Returns(PackageId);
                _validationRequest.Setup(x => x.PackageKey).Returns(PackageKey);
                _validationRequest.Setup(x => x.PackageVersion).Returns(PackageVersion);
                _validationRequest.Setup(x => x.ValidationId).Returns(ValidationId);
                
                _target = new SymbolsIngester(
                    _symbolsValidationEntitiesService.Object,
                    _symbolMessageEnqueuer.Object,
                    _telemetryService.Object,
                    _logger);
            }

            public static SymbolsPackageIngestRequestStatus ConvertToSymbolsPackageIngestRequestStatus(ValidationStatus validationStatus)
            {
                switch(validationStatus)
                {
                    case ValidationStatus.Failed:
                        return SymbolsPackageIngestRequestStatus.FailedIngestion;
                    case ValidationStatus.Succeeded:
                        return SymbolsPackageIngestRequestStatus.Ingested;
                    case ValidationStatus.Incomplete:
                        return SymbolsPackageIngestRequestStatus.Ingesting;
                    default:
                        throw new NotSupportedException($"Not supported {nameof(validationStatus)}");
                }
            }
        }
    }
}
