// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Catalog;
using NuGet.Protocol.Registration;
using Xunit;

namespace NuGet.Jobs.Catalog2Registration
{
    public class RegistrationIndexSponsorshipUrlsFacts
    {
        public class Serialization
        {
            private static RegistrationIndex CreateIndexWithSponsorshipUrls(List<string> urls)
            {
                return new RegistrationIndex
                {
                    Metadata = new Dictionary<string, object>
                    {
                        { RegistrationIndex.SponsorshipUrlsMetadataKey, urls },
                    },
                };
            }

            private static string SerializeWithProductionSerializer(RegistrationIndex index)
            {
                using (var stringWriter = new StringWriter())
                {
                    NuGetJsonSerialization.Serializer.Serialize(stringWriter, index);
                    return stringWriter.ToString();
                }
            }

            [Fact]
            public void NestsSponsorshipUrlsUnderMetadataContainer()
            {
                // Arrange
                var index = CreateIndexWithSponsorshipUrls(new List<string> { "https://example.test/sponsor" });

                // Act
                var json = JsonConvert.SerializeObject(index);
                var document = JObject.Parse(json);

                // Assert
                Assert.NotNull(document.Property("metadata"));
                var sponsorshipUrls = document["metadata"]?["sponsorshipUrls"];
                Assert.Equal(JTokenType.Array, sponsorshipUrls?.Type);
            }

            [Fact]
            public void SerializeEmptyArrayWhenListIsEmpty()
            {
                // Arrange
                var index = CreateIndexWithSponsorshipUrls(new List<string>());

                // Act
                var json = JsonConvert.SerializeObject(index);
                var document = JObject.Parse(json);

                // Assert
                var sponsorshipUrls = document["metadata"]?["sponsorshipUrls"];
                Assert.Equal(JTokenType.Array, sponsorshipUrls?.Type);
                Assert.Empty(sponsorshipUrls);
            }

            [Fact]
            public void SerializeUrlsInOrderWhenPopulated()
            {
                // Arrange
                var index = CreateIndexWithSponsorshipUrls(new List<string>
                {
                    "https://example.test/sponsor1",
                    "https://example.test/sponsor2",
                    "https://example.test/sponsor3"
                });

                // Act
                var json = JsonConvert.SerializeObject(index);
                var document = JObject.Parse(json);
                var urls = (JArray)document["metadata"]["sponsorshipUrls"];

                // Assert
                Assert.Equal("https://example.test/sponsor1", urls[0]?.Value<string>());
                Assert.Equal("https://example.test/sponsor2", urls[1]?.Value<string>());
                Assert.Equal("https://example.test/sponsor3", urls[2]?.Value<string>());
            }

            [Fact]
            public void OmitsMetadataContainerWhenNull()
            {
                // Arrange
                var index = new RegistrationIndex
                {
                    Metadata = null,
                };

                // Act: the production serializer omits null properties.
                var json = SerializeWithProductionSerializer(index);
                var document = JObject.Parse(json);

                // Assert
                Assert.Null(document.Property("metadata"));
            }
        }
    }
}
