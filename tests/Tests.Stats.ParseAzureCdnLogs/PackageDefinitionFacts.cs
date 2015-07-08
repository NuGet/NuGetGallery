// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Stats.AzureCdnLogs.Common;
using Xunit;

namespace Tests.Stats.ParseAzureCdnLogs
{
    public class PackageDefinitionFacts
    {
        [Theory]
        [InlineData("nuget.core", "1.0.1-beta1", "http://localhost/packages/nuget.core.1.0.1-beta1.nupkg")]
        [InlineData("nuget.core", "1.0.1", "http://localhost/packages/nuget.core.1.0.1.nupkg")]
        [InlineData("nuget.core", "1.0", "http://localhost/packages/nuget.core.1.0.nupkg")]
        public void ExtractsPackageIdAndVersionFromRequestUrl(string expectedPackageId, string expectedPackageVersion, string requestUrl)
        {
            var packageDefinition = PackageDefinition.FromRequestUrl(requestUrl);
            Assert.Equal(expectedPackageId, packageDefinition.PackageId);
            Assert.Equal(expectedPackageVersion, packageDefinition.PackageVersion);
        }

        [Fact]
        public void ReturnsNullWhenInvalidPackageRequestUrl()
        {
            var packageDefinition = PackageDefinition.FromRequestUrl("http://localhost/api/v3/index.json");
            Assert.Null(packageDefinition);
        }
    }
}