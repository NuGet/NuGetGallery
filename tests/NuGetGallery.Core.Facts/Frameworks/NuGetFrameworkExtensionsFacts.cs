// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;
using Xunit;

namespace NuGetGallery.Frameworks
{
    public class NuGetFrameworkExtensionsFacts
    {
        [Theory]
        [InlineData("", "0.0")]
        [InlineData("0", "0.0")]
        [InlineData("1", "1.0")]
        [InlineData("01", "0.1")]
        [InlineData("12", "1.2")]
        [InlineData("1.2", "1.2")]
        [InlineData("12.3", "12.3")]
        [InlineData("1.23", "1.23")]
        [InlineData("1.234", "1.234")]
        [InlineData("123", "1.2.3")]
        [InlineData("1.2.3", "1.2.3")]
        [InlineData("1.2.34", "1.2.34")]
        [InlineData("1234", "1.2.3.4")]
        [InlineData("1.2.3.4", "1.2.3.4")]
        [InlineData("1.2.3.45", "1.2.3.45")]
        public void VersionShouldReturnParsedBadgeVersion(string version, string badgeVersion)
        {
            var framework = NuGetFramework.Parse($"net{version}");

            Assert.Equal(badgeVersion, framework.GetBadgeVersion());
        }
    }
}
