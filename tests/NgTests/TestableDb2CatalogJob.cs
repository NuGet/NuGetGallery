// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Ng.Jobs;
using NgTests.Infrastructure;
using NuGet.Services.Logging;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit.Abstractions;

namespace NgTests
{
    public class TestableDb2CatalogJob
        : Db2CatalogJob
    {
        private readonly HttpMessageHandler _handler;

        public TestableDb2CatalogJob(
            HttpMessageHandler handler,
            IStorage catalogStorage,
            IStorage auditingStorage,
            bool skipCreatedPackagesProcessing,
            DateTime? startDate,
            TimeSpan timeout,
            int top,
            bool verbose,
            Mock<IGalleryDatabaseQueryService> galleryDatabaseMock,
            PackageContentUriBuilder packageContentUriBuilder,
            ITestOutputHelper testOutputHelper)
            : base(new TestLoggerFactory(testOutputHelper), new Mock<ITelemetryClient>().Object, new Dictionary<string, string>())
        {
            _handler = handler;

            CatalogStorage = catalogStorage;
            AuditingStorage = auditingStorage;
            SkipCreatedPackagesProcessing = skipCreatedPackagesProcessing;
            StartDate = startDate;
            Timeout = timeout;
            Top = top;
            Verbose = verbose;
            Destination = new Uri("https://nuget.test");

            PackageContentUriBuilder = packageContentUriBuilder ?? throw new ArgumentNullException(nameof(galleryDatabaseMock));
            GalleryDatabaseQueryService = galleryDatabaseMock?.Object ?? throw new ArgumentNullException(nameof(galleryDatabaseMock));
        }

        protected override HttpClient CreateHttpClient()
        {
            return new HttpClient(_handler);
        }

        public async Task RunOnceAsync(CancellationToken cancellationToken)
        {
            await RunInternalAsync(cancellationToken);
        }
    }
}