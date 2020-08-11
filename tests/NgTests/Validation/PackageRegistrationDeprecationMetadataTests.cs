// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Monitoring.Model;
using Xunit;

namespace NgTests.Validation
{
    public class PackageRegistrationDeprecationMetadataTests
    {
        public class TheConstructorWithPackageDeprecationItem
        {
            [Fact]
            public void SingleReason()
            {
                var deprecation = new PackageDeprecationItem(new[] { "a" }, null, null, null);

                var metadata = new PackageRegistrationDeprecationMetadata(deprecation);

                Assert.Equal(deprecation.Reasons, metadata.Reasons);
                Assert.Null(metadata.Message);
                Assert.Null(metadata.AlternatePackage);
            }

            [Fact]
            public void MultipleReasons()
            {
                var deprecation = new PackageDeprecationItem(new[] { "a", "b" }, null, null, null);

                var metadata = new PackageRegistrationDeprecationMetadata(deprecation);

                Assert.Equal(deprecation.Reasons, metadata.Reasons);
                Assert.Null(metadata.Message);
                Assert.Null(metadata.AlternatePackage);
            }

            [Fact]
            public void Message()
            {
                var deprecation = new PackageDeprecationItem(new[] { "c" }, "mmm", null, null);

                var metadata = new PackageRegistrationDeprecationMetadata(deprecation);

                Assert.Equal(deprecation.Reasons, metadata.Reasons);
                Assert.Equal(deprecation.Message, metadata.Message);
                Assert.Null(metadata.AlternatePackage);
            }

            [Fact]
            public void AlternatePackage()
            {
                var deprecation = new PackageDeprecationItem(new[] { "d" }, null, "abc", "cba");

                var metadata = new PackageRegistrationDeprecationMetadata(deprecation);

                Assert.Equal(deprecation.Reasons, metadata.Reasons);
                Assert.Null(metadata.Message);
                Assert.NotNull(metadata.AlternatePackage);
                Assert.Equal(deprecation.AlternatePackageId, metadata.AlternatePackage.Id);
                Assert.Equal(deprecation.AlternatePackageRange, metadata.AlternatePackage.Range);
            }
        }
    }
}
