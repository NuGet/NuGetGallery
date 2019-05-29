// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Ng.Jobs;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace NgTests
{
    public class Package2CatalogJobTests
    {
        private const string _feedBaseUri = "http://unit.test";
        private const string _feedUriSuffix = "/Packages?$filter=Id%20eq%20'{0}'%20and%20Version%20eq%20'{1}'&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl";
        private readonly ITestOutputHelper _testOutputHelper;

        public Package2CatalogJobTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task RunInternal_CallsCatalogStorageLoadStringExactlyOnce()
        {
            var messageHandler = new MockServerHttpClientHandler();
            var catalogStorage = new Mock<IStorage>(MockBehavior.Strict);
            var datetime = DateTime.MinValue.ToString("O") + "Z";
            var json = $"{{\"nuget:lastCreated\":\"{datetime}\"," +
                $"\"nuget:lastDeleted\":\"{datetime}\"," +
                $"\"nuget:lastEdited\":\"{datetime}\"}}";
            var packageId = "a";
            var packageVersion = "1.0.0";

            catalogStorage.Setup(x => x.ResolveUri(It.IsNotNull<string>()))
                .Returns(new Uri(_feedBaseUri));
            catalogStorage.Setup(x => x.LoadStringAsync(It.IsNotNull<Uri>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(json);

            messageHandler.SetAction("/", GetRootActionAsync);
            messageHandler.SetAction(GetPath(packageId, packageVersion), GetEmptyPackages);

            var job = new TestPackage2CatalogJob(
                _testOutputHelper,
                messageHandler,
                catalogStorage.Object,
                _feedBaseUri,
                packageId,
                packageVersion,
                verbose: true);

            await job.RunOnceAsync(CancellationToken.None);

            catalogStorage.Verify(x => x.ResolveUri(It.IsNotNull<string>()), Times.AtLeastOnce());
            catalogStorage.Verify(x => x.LoadStringAsync(It.IsNotNull<Uri>(), It.IsAny<CancellationToken>()), Times.Once());
        }

        private static string GetPath(string packageId, string packageVersion)
        {
            return string.Format(CultureInfo.InvariantCulture, _feedUriSuffix, packageId, packageVersion);
        }

        private static Task<HttpResponseMessage> GetEmptyPackages(HttpRequestMessage request)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    ODataFeedHelper.ToODataFeed(Enumerable.Empty<ODataPackage>(), new Uri(_feedBaseUri), "Packages"))
            });
        }

        private static Task<HttpResponseMessage> GetRootActionAsync(HttpRequestMessage request)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }

        private sealed class TestPackage2CatalogJob : Package2CatalogJob
        {
            private readonly HttpMessageHandler _handler;

            internal TestPackage2CatalogJob(
                ITestOutputHelper testOutputHelper,
                HttpMessageHandler handler,
                IStorage storage,
                string gallery,
                string packageId,
                string packageVersion,
                bool verbose) : base(
                    new Mock<ITelemetryService>().Object,
                    new TestLoggerFactory(testOutputHelper),
                    storage,
                    gallery,
                    packageId,
                    packageVersion,
                    verbose)
            {
                _handler = handler;
            }

            protected override HttpClient CreateHttpClient()
            {
                return new HttpClient(_handler);
            }

            internal Task RunOnceAsync(CancellationToken cancellationToken)
            {
                return RunInternalAsync(cancellationToken);
            }
        }
    }
}