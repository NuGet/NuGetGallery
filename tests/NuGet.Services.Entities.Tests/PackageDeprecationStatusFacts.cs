// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGet.Services.Entities.Tests
{
    public class PackageDeprecationStatusFacts
    {
        /// <summary>
        /// These values are immutable since they are persisted to the database. This dictionary should only ever be
        /// appended to as new <see cref="PackageStatus"/> values are added.
        /// </summary>
        private static readonly IReadOnlyDictionary<PackageDeprecationStatus, int> ImmutablePackageDeprecationStatusValues = new Dictionary<PackageDeprecationStatus, int>
        {
            { PackageDeprecationStatus.NotDeprecated, 0 },
            { PackageDeprecationStatus.Other, 1 },
            { PackageDeprecationStatus.Legacy, 2 },
            { PackageDeprecationStatus.Vulnerable, 4 },
        };

        [Fact]
        public void AssertNotChanged()
        {
            foreach (var pair in ImmutablePackageDeprecationStatusValues)
            {
                Assert.Equal(pair.Value, (int)pair.Key);
            }
        }

        [Fact]
        public void AssertAllValuesAreImmutable()
        {
            var expected = ImmutablePackageDeprecationStatusValues
                .Keys
                .OrderBy(x => x)
                .ToList();

            var actual = Enum
                .GetValues(typeof(PackageDeprecationStatus))
                .Cast<PackageDeprecationStatus>()
                .OrderBy(x => x)
                .ToList();

            Assert.Equal(expected, actual);
        }
    }
}
