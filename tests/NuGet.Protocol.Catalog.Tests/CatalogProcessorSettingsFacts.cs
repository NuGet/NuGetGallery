// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using FluentAssertions;
using Xunit;

namespace NuGet.Protocol.Catalog
{
    public class CatalogProcessorSettingsFacts
    {
        public class Constructor
        {
            [Fact]
            public void HasUnchangingDefaults()
            {
                // Arrange
                var expected = new CatalogProcessorSettings
                {
                    ServiceIndexUrl = "https://api.nuget.org/v3/index.json",
                    DefaultMinCommitTimestamp = null,
                    MinCommitTimestamp = DateTimeOffset.MinValue,
                    MaxCommitTimestamp = DateTimeOffset.MaxValue,
                    ExcludeRedundantLeaves = true,
                };

                // Act
                var actual = new CatalogProcessorSettings();

                // Assert
                actual.Should().BeEquivalentTo(expected);
            }
        }

        public class Clone
        {
            [Fact]
            public void CopiesAllProperties()
            {
                // Arrange
                var expected = new CatalogProcessorSettings
                {
                    ServiceIndexUrl = "https://example/v3/index.json",
                    DefaultMinCommitTimestamp = new DateTimeOffset(2017, 11, 8, 13, 50, 44, TimeSpan.Zero),
                    MinCommitTimestamp = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    MaxCommitTimestamp = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    ExcludeRedundantLeaves = true,
                };

                // Act
                var actual = expected.Clone();

                // Assert
                actual.Should().BeEquivalentTo(expected);
            }
        }
    }
}
