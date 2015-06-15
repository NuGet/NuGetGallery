// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Stats.AzureCdnLogs.Common;
using Xunit;

namespace Tests.Stats.CollectAzureCdnLogs
{
    public class AzureCdnPlatformExtensionsTests
    {
        [Theory]
        [InlineData("wpc", AzureCdnPlatform.HttpLargeObject)]
        [InlineData("wac", AzureCdnPlatform.HttpSmallObject)]
        [InlineData("adn", AzureCdnPlatform.ApplicationDeliveryNetwork)]
        [InlineData("fms", AzureCdnPlatform.FlashMediaStreaming)]
        internal void CanParseValidAzureCdnPlatformStrings(string prefix, AzureCdnPlatform platform)
        {
            var actual = AzureCdnPlatformExtensions.ParseAzureCdnPlatformPrefix(prefix);
            Assert.Equal(platform, actual);
        }

        [Fact]
        public void ParseThrowsForInvalidAzureCdnPlatformPrefix()
        {
            Assert.Throws<UnknownAzureCdnPlatformException>(() => AzureCdnPlatformExtensions.ParseAzureCdnPlatformPrefix("bla"));
        }

        [Theory]
        [InlineData(AzureCdnPlatform.HttpLargeObject, "wpc")]
        [InlineData(AzureCdnPlatform.HttpSmallObject, "wac")]
        [InlineData(AzureCdnPlatform.ApplicationDeliveryNetwork, "adn")]
        [InlineData(AzureCdnPlatform.FlashMediaStreaming, "fms")]
        internal void CanGetRawLogFilePrefixForValidAzureCdnPlatforms(AzureCdnPlatform platform, string prefix)
        {
            var actual = AzureCdnPlatformExtensions.GetRawLogFilePrefix(platform);
            Assert.Equal(prefix, actual);
        }

        [Fact]
        public void GetRawLogFilePrefixThrowsForInvalidAzureCdnPlatform()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AzureCdnPlatformExtensions.GetRawLogFilePrefix((AzureCdnPlatform)4));
        }
    }
}