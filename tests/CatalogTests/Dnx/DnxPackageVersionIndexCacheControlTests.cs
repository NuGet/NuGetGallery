// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Metadata.Catalog.Dnx;
using Xunit;

namespace CatalogTests.Dnx
{
    public class DnxPackageVersionIndexCacheControlTests
    {
        [Theory]
        [InlineData("PackageId1", "max-age=10")]
        [InlineData("BaseTestPackage", "max-age=10")]
        [InlineData("basetestpackage", "no-store")]
        public void GetCacheControl(string packageId, string cacheControl)
        {
            Assert.Equal(cacheControl, DnxPackageVersionIndexCacheControl.GetCacheControl(packageId, Mock.Of<ILogger>()));
        }
    }
}
