// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NuGet.Protocol.Catalog
{
    public class PackageDetailsCatalogLeafFacts
    {
        public class SponsorshipUrlsSerialization
        {
            [Fact]
            public void UsesSponsorshipUrlsJsonPropertyName()
            {
                // Arrange
                var leaf = new PackageDetailsCatalogLeaf
                {
                    Type = CatalogLeafType.PackageDetails,
                    SponsorshipUrls = new List<string> { "https://example.test/sponsor" },
                };

                // Act
                var json = JsonConvert.SerializeObject(leaf);
                var document = JObject.Parse(json);

                // Assert
                Assert.NotNull(document.Property("sponsorshipUrls"));
                Assert.Equal(JTokenType.Array, document["sponsorshipUrls"]?.Type);
            }

            [Fact]
            public void SerializeEmptyArrayWhenListIsEmpty()
            {
                // Arrange
                var leaf = new PackageDetailsCatalogLeaf
                {
                    Type = CatalogLeafType.PackageDetails,
                    SponsorshipUrls = new List<string>(),
                };

                // Act
                var json = JsonConvert.SerializeObject(leaf);
                var document = JObject.Parse(json);

                // Assert
                Assert.NotNull(document.Property("sponsorshipUrls"));
                Assert.Equal(JTokenType.Array, document["sponsorshipUrls"]?.Type);
                Assert.Empty(document["sponsorshipUrls"]);
            }

            [Fact]
            public void SerializeUrlsInOrderWhenPopulated()
            {
                // Arrange
                var leaf = new PackageDetailsCatalogLeaf
                {
                    Type = CatalogLeafType.PackageDetails,
                    SponsorshipUrls = new List<string>
                    {
                        "https://example.test/sponsor1",
                        "https://example.test/sponsor2" ,
                        "https://example.test/sponsor3"
                    },
                };

                // Act
                var json = JsonConvert.SerializeObject(leaf);
                var document = JObject.Parse(json);
                var urls = (JArray)document["sponsorshipUrls"];

                // Assert
                Assert.Equal("https://example.test/sponsor1", urls[0]?.Value<string>());
                Assert.Equal("https://example.test/sponsor2", urls[1]?.Value<string>());
                Assert.Equal("https://example.test/sponsor3", urls[2]?.Value<string>());
            }

            [Fact]
            public void SerializeNullSponsorshipUrlsWhenUnset()
            {
                // Arrange
                var leaf = new PackageDetailsCatalogLeaf
                {
                    Type = CatalogLeafType.PackageDetails,
                    SponsorshipUrls = null,
                };

                // Act
                var json = JsonConvert.SerializeObject(leaf);
                var document = JObject.Parse(json);

                // Assert
                Assert.NotNull(document.Property("sponsorshipUrls"));
                Assert.Equal(JTokenType.Null, document["sponsorshipUrls"]?.Type);
            }
        }
    }
}
