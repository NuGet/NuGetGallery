// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Versioning;

namespace NgTests.Validation
{
    internal sealed class ValidationContextStub : ValidationContext
    {
        private static readonly PackageIdentity _packageIdentity = new PackageIdentity("A", new NuGetVersion(1, 0, 0));

        private ValidationContextStub(
            PackageIdentity package,
            IEnumerable<CatalogIndexEntry> entries,
            IEnumerable<DeletionAuditEntry> deletionAuditEntries,
            ValidationSourceRepositories sourceRepositories,
            CollectorHttpClient client,
            CancellationToken token,
            ILogger<ValidationContext> logger)
            : base(package, entries, deletionAuditEntries, sourceRepositories, client, token, logger)
        {
        }

        internal static ValidationContextStub Create(
            PackageIdentity package = null,
            IEnumerable<CatalogIndexEntry> entries = null,
            IEnumerable<DeletionAuditEntry> deletionAuditEntries = null,
            Dictionary<FeedType, SourceRepository> feedToSource = null,
            CollectorHttpClient client = null,
            HttpClientHandler clientHandler = null,
            CancellationToken? token = null,
            ILogger<ValidationContext> logger = null,
            IPackageTimestampMetadataResource timestampMetadataResource = null,
            IPackageRegistrationMetadataResource v2Resource = null,
            IPackageRegistrationMetadataResource v3Resource = null)
        {
            if (feedToSource == null)
            {
                feedToSource = new Dictionary<FeedType, SourceRepository>();

                var v2Repository = new Mock<SourceRepository>();
                var v3Repository = new Mock<SourceRepository>();

                feedToSource.Add(FeedType.HttpV2, v2Repository.Object);
                feedToSource.Add(FeedType.HttpV3, v3Repository.Object);

                v2Repository.Setup(x => x.GetResource<IPackageTimestampMetadataResource>())
                    .Returns(timestampMetadataResource ?? Mock.Of<IPackageTimestampMetadataResource>());

                v2Repository.Setup(x => x.GetResource<IPackageRegistrationMetadataResource>())
                    .Returns(v2Resource ?? Mock.Of<IPackageRegistrationMetadataResource>());

                v3Repository.Setup(x => x.GetResource<IPackageRegistrationMetadataResource>())
                    .Returns(v3Resource ?? Mock.Of<IPackageRegistrationMetadataResource>());

                v3Repository.Setup(x => x.GetResource<HttpSourceResource>())
                    .Returns(new HttpSourceResource(new HttpSource(
                        new PackageSource("https://example/v3/index.json"),
                        () => Task.FromResult<HttpHandlerResource>(new HttpHandlerResourceV3(
                            clientHandler ?? new HttpClientHandler(),
                            clientHandler ?? Mock.Of<HttpMessageHandler>())),
                        NullThrottle.Instance)));
            }

            var sourceRepositories = new ValidationSourceRepositories(
                feedToSource[FeedType.HttpV2],
                feedToSource[FeedType.HttpV3]);

            return new ValidationContextStub(
                package ?? _packageIdentity,
                entries ?? Enumerable.Empty<CatalogIndexEntry>(),
                deletionAuditEntries ?? Enumerable.Empty<DeletionAuditEntry>(),
                sourceRepositories,
                client ?? new CollectorHttpClient(clientHandler),
                token ?? CancellationToken.None,
                logger ?? Mock.Of<ILogger<ValidationContext>>());
        }
    }
}