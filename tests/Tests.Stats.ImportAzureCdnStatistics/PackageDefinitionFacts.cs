// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Stats.AzureCdnLogs.Common;
using Xunit;

namespace Tests.Stats.ImportAzureCdnStatistics
{
    public class PackageDefinitionFacts
    {
        [Theory]
        [InlineData("nuget.core", "1.0.1-beta1", "http://localhost/packages/nuget.core.1.0.1-beta1.nupkg")]
        [InlineData("nuget.core", "1.0.1", "http://localhost/packages/nuget.core.1.0.1.nupkg")]
        [InlineData("nuget.core", "1.0", "http://localhost/packages/nuget.core.1.0.nupkg")]
        [InlineData("1", "1.0.0", "http://localhost/packages/1.1.0.0.nupkg")]
        [InlineData("Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.ServiceBus", "6.0.1304", "http://localhost/packages/Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.ServiceBus.6.0.1304.nupkg")]
        //[InlineData("5.0.0.0", "5.0.0", "http://localhost/packages/5.0.0.0.5.0.0.nupkg")] -- can't determine for 100% what the correct id and version is without reaching out to the main gallery db
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