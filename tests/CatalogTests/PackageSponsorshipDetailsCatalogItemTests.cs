// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using VDS.RDF;
using Xunit;

namespace CatalogTests
{
    public class PackageSponsorshipDetailsCatalogItemTests
    {
        private static readonly Uri BaseAddress = new Uri("http://example/");
        private static readonly DateTime TimeStamp = new DateTime(2026, 1, 4, 8, 15, 0, DateTimeKind.Utc);
        private static readonly Guid CommitId = new Guid("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Constructor_ThrowsForNullOrEmptyId(string id)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new PackageSponsorshipDetailsCatalogItem(id, new[] { "https://github.com/sponsors/octocat" }));

            Assert.Equal("id", exception.ParamName);
        }

        [Fact]
        public void GetItemType_ReturnsPackageSponsorshipDetails()
        {
            var item = CreateItem("NuGet.Versioning", new[] { "https://github.com/sponsors/octocat" });

            Assert.Equal(new Uri("http://schema.nuget.org/schema#PackageSponsorshipDetails"), item.GetItemType());
        }

        [Fact]
        public void GetItemAddress_IsVersionLessAndLowercased()
        {
            var item = CreateItem("NuGet.Versioning", new[] { "https://github.com/sponsors/octocat" });

            Assert.Equal(
                new Uri("http://example/data/2026.01.04.08.15.00/nuget.versioning.json"),
                item.GetItemAddress());
        }

        [Fact]
        public void CreateContent_ProducesExpectedJson()
        {
            var item = CreateItem(
                "NuGet.Versioning",
                new[] { "https://github.com/sponsors/octocat", "https://opencollective.com/nuget" });

            var json = CreateContentJson(item);

            Assert.Contains("PackageSponsorshipDetails", json["@type"].Values<string>());
            Assert.Equal("NuGet.Versioning", (string)json["id"]);
            Assert.Equal("NuGet.Versioning", (string)json["originalId"]);
            Assert.Equal(CommitId.ToString(), (string)json["catalog:commitId"]);

            var urls = json["sponsorshipUrls"].Values<string>().ToList();
            Assert.Equal(2, urls.Count);
            Assert.Contains("https://github.com/sponsors/octocat", urls);
            Assert.Contains("https://opencollective.com/nuget", urls);
        }

        [Fact]
        public void CreateContent_EmitsSponsorshipUrlsAsArrayForSingleUrl()
        {
            var item = CreateItem("NuGet.Versioning", new[] { "https://github.com/sponsors/octocat" });

            var json = CreateContentJson(item);

            Assert.Equal(JTokenType.Array, json["sponsorshipUrls"].Type);
            Assert.Equal("https://github.com/sponsors/octocat", json["sponsorshipUrls"].Single().Value<string>());
        }

        [Fact]
        public void CreateContent_OmitsSponsorshipUrlsWhenEmpty()
        {
            // A retraction: the leaf is still emitted (so the registration lane clears the root field),
            // but it carries no sponsorship URLs.
            var item = CreateItem("NuGet.Versioning", Array.Empty<string>());

            var json = CreateContentJson(item);

            Assert.Contains("PackageSponsorshipDetails", json["@type"].Values<string>());
            Assert.Equal("NuGet.Versioning", (string)json["id"]);
            Assert.Null(json["sponsorshipUrls"]);
        }

        [Fact]
        public void CreateContent_OmitsSponsorshipUrlsWhenNull()
        {
            var item = CreateItem("NuGet.Versioning", sponsorshipUrls: null);

            var json = CreateContentJson(item);

            Assert.Null(json["sponsorshipUrls"]);
        }

        [Fact]
        public void CreatePageContent_EmitsSingleIdTriple()
        {
            var item = CreateItem("NuGet.Versioning", new[] { "https://github.com/sponsors/octocat" });

            var pageContent = item.CreatePageContent(new CatalogContext());

            var triples = pageContent.Triples.Cast<Triple>().ToList();
            var triple = Assert.Single(triples);
            Assert.Equal(Schema.Predicates.Id.ToString(), triple.Predicate.ToString());
            Assert.Equal("NuGet.Versioning", triple.Object.ToString());
        }

        private static PackageSponsorshipDetailsCatalogItem CreateItem(string id, string[] sponsorshipUrls)
        {
            return new PackageSponsorshipDetailsCatalogItem(id, sponsorshipUrls)
            {
                BaseAddress = BaseAddress,
                TimeStamp = TimeStamp,
                CommitId = CommitId,
            };
        }

        private static JObject CreateContentJson(PackageSponsorshipDetailsCatalogItem item)
        {
            var content = item.CreateContent(new CatalogContext());
            using (var reader = new StreamReader(content.GetContentStream()))
            {
                return JObject.Parse(reader.ReadToEnd());
            }
        }
    }
}
