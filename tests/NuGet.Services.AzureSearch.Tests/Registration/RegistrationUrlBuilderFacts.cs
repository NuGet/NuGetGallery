// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGet.Protocol.Registration
{
    public class RegistrationUrlBuilderFacts
    {
        public class GetIndexUrl
        {
            [Theory]
            [InlineData("https://ex/reg", "NuGet.Core", "https://ex/reg/nuget.core/index.json")]
            [InlineData("https://ex/reg/", "NuGet.Core", "https://ex/reg/nuget.core/index.json")]
            [InlineData("https://ex/reg//", "NuGet.Core", "https://ex/reg/nuget.core/index.json")]
            public void ReturnsExpectedUrl(string baseUrl, string id, string expected)
            {
                var actual = RegistrationUrlBuilder.GetIndexUrl(baseUrl, id);

                Assert.Equal(expected, actual);
            }
        }

        public class GetLeafUrl
        {
            [Theory]
            [InlineData("https://ex/reg", "NuGet.Core", "3.0.0", "https://ex/reg/nuget.core/3.0.0.json")]
            [InlineData("https://ex/reg/", "NuGet.Core", "3.0.0", "https://ex/reg/nuget.core/3.0.0.json")]
            [InlineData("https://ex/reg//", "NuGet.Core", "3.0.0", "https://ex/reg/nuget.core/3.0.0.json")]
            [InlineData("https://ex/reg/", "NuGet.Core", "3.0.0+git", "https://ex/reg/nuget.core/3.0.0.json")]
            [InlineData("https://ex/reg/", "NuGet.Core", "3.0.0.0", "https://ex/reg/nuget.core/3.0.0.json")]
            [InlineData("https://ex/reg/", "NuGet.Core", "3.0.0.0-ALPHA", "https://ex/reg/nuget.core/3.0.0-alpha.json")]
            [InlineData("https://ex/reg/", "NuGet.Core", "3.0.00-ALPHA.1+foo", "https://ex/reg/nuget.core/3.0.0-alpha.1.json")]
            public void ReturnsExpectedUrl(string baseUrl, string id, string version, string expected)
            {
                var actual = RegistrationUrlBuilder.GetLeafUrl(baseUrl, id, version);

                Assert.Equal(expected, actual);
            }
        }
    }
}
