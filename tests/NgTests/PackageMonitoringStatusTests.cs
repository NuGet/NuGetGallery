// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Versioning;
using Xunit;

namespace NgTests
{
    public class PackageMonitoringStatusTests
    {
        public class WithPackageValidationResult
        {
            [Fact]
            public void ThrowsIfNullArgument()
            {
                Assert.Throws<ArgumentNullException>(() => new PackageMonitoringStatus(null));
            }

            [Fact]
            public void Valid()
            {
                var validationResults = new ValidationResult[]
                {
                    new ValidationResult(null, TestResult.Pass),
                    new ValidationResult(null, TestResult.Skip)
                };

                var aggregateValidationResults = new AggregateValidationResult(
                    null,
                    validationResults);

                var packageValidationResult = new PackageValidationResult(
                    new PackageIdentity("testPackage", new NuGetVersion(4, 5, 6)),
                    null,
                    null,
                    new AggregateValidationResult[] { aggregateValidationResults });

                var status = new PackageMonitoringStatus(packageValidationResult);

                Assert.Equal(PackageState.Valid, status.State);
            }

            [Fact]
            public void Invalid()
            {
                var validationResults = new ValidationResult[]
                {
                    new ValidationResult(null, TestResult.Fail)
                };

                var aggregateValidationResults = new AggregateValidationResult(
                    null,
                    validationResults);

                var packageValidationResult = new PackageValidationResult(
                    new PackageIdentity("testPackage", new NuGetVersion(4, 5, 6)),
                    null,
                    null,
                    new AggregateValidationResult[] { aggregateValidationResults });

                var status = new PackageMonitoringStatus(packageValidationResult);

                Assert.Equal(PackageState.Invalid, status.State);
            }

            [Fact]
            public void Unknown()
            {
                var validationResults = new ValidationResult[]
                {
                    new ValidationResult(null, TestResult.Pass),
                    new ValidationResult(null, TestResult.Pending)
                };

                var aggregateValidationResults = new AggregateValidationResult(
                    null,
                    validationResults);

                var packageValidationResult = new PackageValidationResult(
                    new PackageIdentity("testPackage", new NuGetVersion(4, 5, 6)),
                    null,
                    null,
                    new AggregateValidationResult[] { aggregateValidationResults });

                var status = new PackageMonitoringStatus(packageValidationResult);

                Assert.Equal(PackageState.Unknown, status.State);
            }
        }

        public class WithException
        {
            [Fact]
            public void ThrowsIfNullArgument()
            {
                Assert.Throws<ArgumentNullException>(
                    () => new PackageMonitoringStatus(new FeedPackageIdentity("hi", "1.0.0"), null));
                Assert.Throws<ArgumentNullException>(
                    () => new PackageMonitoringStatus(null, new Exception()));
                Assert.Throws<ArgumentNullException>(
                    () => new PackageMonitoringStatus(null, null));
            }

            [Fact]
            public void Invalid()
            {
                var status = new PackageMonitoringStatus(new FeedPackageIdentity("hello", "2.1.0"), new Exception());

                Assert.Equal(PackageState.Invalid, status.State);
            }
        }
    }
}
