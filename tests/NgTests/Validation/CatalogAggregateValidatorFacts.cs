// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using NgTests.Infrastructure;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Metadata.Catalog.Monitoring.Validation.Test.Catalog;
using NuGet.Services.Metadata.Catalog.Monitoring.Validation.Test.Exceptions;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using Xunit;

namespace NgTests.Validators
{
    public class CatalogAggregateValidatorFacts
    {
        private static readonly Uri _baseUri = new Uri("https://nuget.test");
        private static readonly PackageIdentity _packageIdentity = new PackageIdentity("A", new NuGetVersion(1, 0, 0));
        private static readonly Uri _packageDetailsUri = new Uri($"{_baseUri.AbsoluteUri}{_packageIdentity.Id.ToLowerInvariant()}");
        private static readonly ValidatorConfiguration _validatorConfiguration = new ValidatorConfiguration(packageBaseAddress: "A", requirePackageSignature: false);

        [Fact]
        public void Constructor_WhenFactoryIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new CatalogAggregateValidator(factory: null, configuration: _validatorConfiguration));

            Assert.Equal("factory", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenConfigurationIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new CatalogAggregateValidator(
                    new ValidatorFactory(
                        Mock.Of<IDictionary<FeedType, SourceRepository>>(),
                        _validatorConfiguration,
                        Mock.Of<ILoggerFactory>()),
                    configuration: null));

            Assert.Equal("configuration", exception.ParamName);
        }

        [Fact]
        public async Task Name_Always_ReturnsTypeName()
        {
            using (var clientHandler = new MockServerHttpClientHandler())
            using (CollectorHttpClient client = await CreateCollectorHttpClientStubAsync(
                clientHandler,
                new MemoryStorage()))
            {
                ValidationContext context = CreateContext(client, DateTime.UtcNow);
                CatalogAggregateValidator validator = CreateValidator(
                    context,
                    DateTime.UtcNow,
                    requirePackageSignature: false);

                Assert.Equal(typeof(CatalogAggregateValidator).FullName, validator.Name);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ValidateAsync_WhenRequirePackageSignatureIsFalse_DoesNotRequirePackageSignatureFile(
            bool includePackageSignatureFile)
        {
            var storage = new MemoryStorage(_baseUri);
            var now = GetDateTime();
            var storageContent = CreateStorageContent(now, includePackageSignatureFile);

            storage.Content.TryAdd(_packageDetailsUri, storageContent);

            using (var clientHandler = new MockServerHttpClientHandler())
            using (CollectorHttpClient client = await CreateCollectorHttpClientStubAsync(clientHandler, storage))
            {
                ValidationContext context = CreateContext(client, now);
                CatalogAggregateValidator validator = CreateValidator(context, now, requirePackageSignature: false);

                AggregateValidationResult result = await validator.ValidateAsync(context);

                Assert.Empty(result.ValidationResults);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ValidateAsync_WhenRequirePackageSignatureIsTrue_RequiresPackageSignatureFile(
            bool includePackageSignatureFile)
        {
            var storage = new MemoryStorage(_baseUri);
            var now = GetDateTime();
            var storageContent = CreateStorageContent(now, includePackageSignatureFile);

            storage.Content.TryAdd(_packageDetailsUri, storageContent);

            using (var clientHandler = new MockServerHttpClientHandler())
            using (CollectorHttpClient client = await CreateCollectorHttpClientStubAsync(clientHandler, storage))
            {
                ValidationContext context = CreateContext(client, now);
                CatalogAggregateValidator validator = CreateValidator(context, now, requirePackageSignature: true);

                AggregateValidationResult result = await validator.ValidateAsync(context);

                Assert.Single(result.ValidationResults);

                var expectedResult = includePackageSignatureFile ? TestResult.Pass : TestResult.Fail;
                var actualResult = result.ValidationResults.Single();

                Assert.Equal(expectedResult, actualResult.Result);

                if (includePackageSignatureFile)
                {
                    Assert.Null(actualResult.Exception);
                }
                else
                {
                    Assert.IsType<MissingPackageSignatureFileException>(actualResult.Exception);
                }
            }
        }

        private static JTokenStorageContent CreateStorageContent(DateTime now, bool includePackageSignatureFile)
        {
            var value = now.ToString(CatalogConstants.DateTimeFormat);

            var packageEntries = new JArray();

            var jObject = new JObject(
                new JProperty(CatalogConstants.Created, value),
                new JProperty(CatalogConstants.LastEdited, value),
                new JProperty(CatalogConstants.PackageEntries, packageEntries));

            if (includePackageSignatureFile)
            {
                packageEntries.Add(
                    new JObject(
                        new JProperty(CatalogConstants.FullName, SigningSpecifications.V1.SignaturePath)));
            }

            return new JTokenStorageContent(jObject);
        }

        private static CatalogAggregateValidator CreateValidator(
            ValidationContext context,
            DateTime now,
            bool requirePackageSignature)
        {
            var feedToSource = new Mock<IDictionary<FeedType, SourceRepository>>();
            var config = ValidatorTestUtility.CreateValidatorConfig(requirePackageSignature: requirePackageSignature);
            var loggerFactory = CreateLoggerFactory();
            var validatorFactory = new ValidatorFactory(feedToSource.Object, config, loggerFactory);
            var sourceRepository = new Mock<SourceRepository>();
            var metadataResource = new Mock<IPackageTimestampMetadataResource>();

            metadataResource.Setup(x => x.GetAsync(It.Is<ValidationContext>(vc => vc == context)))
                .ReturnsAsync(PackageTimestampMetadata.CreateForPackageExistingOnFeed(now, now));

            sourceRepository.Setup(x => x.GetResource<IPackageTimestampMetadataResource>())
                .Returns(metadataResource.Object);

            feedToSource.Setup(x => x[It.IsAny<FeedType>()]).Returns(sourceRepository.Object);

            return new CatalogAggregateValidator(validatorFactory, config);
        }

        private static ValidationContext CreateContext(CollectorHttpClient client, DateTime commitTimeStamp)
        {
            var catalogEntries = new[]
            {
                new CatalogIndexEntry(
                    _packageDetailsUri,
                    CatalogConstants.NuGetPackageDetails,
                    Guid.NewGuid().ToString(),
                    commitTimeStamp,
                    _packageIdentity)
            };

            return new ValidationContext(
                _packageIdentity,
                catalogEntries,
                Enumerable.Empty<DeletionAuditEntry>(),
                client,
                CancellationToken.None);
        }

        private static ILoggerFactory CreateLoggerFactory()
        {
            var loggerFactory = new Mock<ILoggerFactory>();

            loggerFactory
                .Setup(x => x.CreateLogger(It.IsAny<string>()))
                .Returns(() => new Mock<ILogger>().Object);

            return loggerFactory.Object;
        }

        private static async Task<CollectorHttpClient> CreateCollectorHttpClientStubAsync(
            MockServerHttpClientHandler clientHandler,
            MemoryStorage catalogStorage)
        {
            clientHandler.SetAction("/", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

            await clientHandler.AddStorageAsync(catalogStorage);

            return new CollectorHttpClient(clientHandler);
        }

        private static DateTime GetDateTime()
        {
            var now = DateTime.UtcNow;

            // Ensure that the value is round-trippable with catalog datetime format.
            return DateTime.Parse(now.ToString(CatalogConstants.DateTimeFormat));
        }
    }
}