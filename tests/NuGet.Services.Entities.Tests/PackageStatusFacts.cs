// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGet.Services.Entities.Tests
{
    public class PackageStatusFacts
    {
        /// <summary>
        /// These values are immutable since they are persisted to the database. This dictionary should only ever be
        /// appended to as new <see cref="PackageStatus"/> values are added.
        /// </summary>
        private static readonly IReadOnlyDictionary<PackageStatus, int> ImmutablePackageStatusValues = new Dictionary<PackageStatus, int>
        {
            { PackageStatus.Available, 0 },
            { PackageStatus.Deleted, 1 },
            { PackageStatus.Validating, 2 },
            { PackageStatus.FailedValidation, 3 },
        };

        [Fact]
        public void AssertNotChanged()
        {
            foreach (var pair in ImmutablePackageStatusValues)
            {
                Assert.Equal(pair.Value, (int)pair.Key);
            }
        }

        [Fact]
        public void AssertAllValuesAreImmutable()
        {
            var expected = ImmutablePackageStatusValues
                .Keys
                .OrderBy(x => x)
                .ToList();

            var actual = Enum
                .GetValues(typeof(PackageStatus))
                .Cast<PackageStatus>()
                .OrderBy(x => x)
                .ToList();

            Assert.Equal(expected, actual);
        }
    }
}