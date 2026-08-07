// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Services;
using NuGet.Services.Metadata.Catalog;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Jobs.Catalog2Registration
{
    public class RegistrationLevelCollectorFacts
    {
        public class TheFetchAsyncMethod : Facts
        {
            public TheFetchAsyncMethod(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task ReadsIdLevelLeafAndAppliesSponsorshipUrls()
            {
                await Target.RunAsync(MemoryCursor.CreateMin(), MemoryCursor.CreateMax(), CancellationToken.None);

                Updater.Verify(
                    x => x.UpdateAsync(
                        "NuGet.Versioning",
                        It.Is<IReadOnlyList<string>>(urls => urls.Count == 1 && urls[0] == "https://github.com/sponsors/nuget"),
                        It.IsAny<DateTimeOffset>()),
                    Times.Once);
            }

            [Fact]
            public async Task IgnoresVersionLevelItems()
            {
                await Target.RunAsync(MemoryCursor.CreateMin(), MemoryCursor.CreateMax(), CancellationToken.None);

                // Only the single ID-level item should be processed; the version-level item on the same page is skipped.
                Updater.Verify(
                    x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<DateTimeOffset>()),
                    Times.Once);
            }

            [Fact]
            public async Task TreatsMissingSponsorshipUrlsAsRetraction()
            {
                LeafSponsorshipUrls = null;

                await Target.RunAsync(MemoryCursor.CreateMin(), MemoryCursor.CreateMax(), CancellationToken.None);

                Updater.Verify(
                    x => x.UpdateAsync(
                        "NuGet.Versioning",
                        It.Is<IReadOnlyList<string>>(urls => urls.Count == 0),
                        It.IsAny<DateTimeOffset>()),
                    Times.Once);
            }
        }

        public abstract class Facts
        {
            private const string IndexUrl = "https://example/catalog/index.json";
            private const string PageUrl = "https://example/catalog/page0.json";
            private const string SponsorshipLeafUrl = "https://example/catalog/data/sponsorship/nuget.versioning.json";
            private const string VersionLeafUrl = "https://example/catalog/data/version/nuget.versioning.1.0.0.json";
            private const string SponsorshipType = "http://schema.nuget.org/schema#PackageSponsorshipDetails";
            private const string PackageDetailsType = "http://schema.nuget.org/schema#PackageDetails";

            public Facts(ITestOutputHelper output)
            {
                Updater = new Mock<IRegistrationLevelUpdater>();
                TelemetryService = new Mock<ITelemetryService>();
                Options = new Mock<IOptionsSnapshot<Catalog2RegistrationConfiguration>>();
                Config = new Catalog2RegistrationConfiguration
                {
                    Source = IndexUrl,
                    HttpClientTimeout = TimeSpan.FromMinutes(5),
                };
                Options.Setup(x => x.Value).Returns(() => Config);
                CommitTimeStamp = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
                LeafSponsorshipUrls = new[] { "https://github.com/sponsors/nuget" };

                Target = new RegistrationLevelCollector(
                    Updater.Object,
                    TelemetryService.Object,
                    () => new RoutingHandler(this),
                    Options.Object);
            }

            public Mock<IRegistrationLevelUpdater> Updater { get; }
            public Mock<ITelemetryService> TelemetryService { get; }
            public Mock<IOptionsSnapshot<Catalog2RegistrationConfiguration>> Options { get; }
            public Catalog2RegistrationConfiguration Config { get; }
            public DateTimeOffset CommitTimeStamp { get; }
            public string[] LeafSponsorshipUrls { get; set; }
            public RegistrationLevelCollector Target { get; }

            private JObject CreateIndex()
            {
                return new JObject(
                    new JProperty("items", new JArray(
                        new JObject(
                            new JProperty("@id", PageUrl),
                            new JProperty("commitTimeStamp", CommitTimeStamp.ToString("O"))))));
            }

            private JObject CreatePage()
            {
                return new JObject(
                    new JProperty("@context", new JObject()),
                    new JProperty("items", new JArray(
                        new JObject(
                            new JProperty("@id", SponsorshipLeafUrl),
                            new JProperty("@type", SponsorshipType),
                            new JProperty("commitTimeStamp", CommitTimeStamp.ToString("O")),
                            new JProperty("nuget:id", "NuGet.Versioning")),
                        new JObject(
                            new JProperty("@id", VersionLeafUrl),
                            new JProperty("@type", PackageDetailsType),
                            new JProperty("commitTimeStamp", CommitTimeStamp.ToString("O")),
                            new JProperty("nuget:id", "NuGet.Versioning"),
                            new JProperty("nuget:version", "1.0.0")))));
            }

            private JObject CreateLeaf()
            {
                var leaf = new JObject(new JProperty("id", "NuGet.Versioning"));
                if (LeafSponsorshipUrls != null)
                {
                    leaf.Add(new JProperty("sponsorshipUrls", new JArray(LeafSponsorshipUrls)));
                }

                return leaf;
            }

            private sealed class RoutingHandler : HttpMessageHandler
            {
                private readonly Facts _facts;

                public RoutingHandler(Facts facts)
                {
                    _facts = facts;
                }

                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var path = request.RequestUri.GetLeftPart(UriPartial.Path);
                    JObject body;
                    switch (path)
                    {
                        case IndexUrl:
                            body = _facts.CreateIndex();
                            break;
                        case PageUrl:
                            body = _facts.CreatePage();
                            break;
                        case SponsorshipLeafUrl:
                            body = _facts.CreateLeaf();
                            break;
                        default:
                            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
                    }

                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(body.ToString()),
                    });
                }
            }
        }
    }
}
