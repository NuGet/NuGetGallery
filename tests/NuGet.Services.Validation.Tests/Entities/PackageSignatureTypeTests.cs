// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGet.Services.Validation.Tests.Entities
{
    public class PackageSignatureTypeTests
    {
        private static readonly IReadOnlyDictionary<int, PackageSignatureType> Expected = new Dictionary<int, PackageSignatureType>
        {
            { 1, PackageSignatureType.Author },
            { 2, PackageSignatureType.Repository },
        };

        [Theory]
        [MemberData(nameof(HasExpectedValuesData))]
        public void HasExpectedValues(int expected, PackageSignatureType actual)
        {
            Assert.Equal((PackageSignatureType)expected, actual);
        }

        [Fact]
        public void HasAllValuesTested()
        {
            Assert.Equal(
                Expected.Values.OrderBy(x => x),
                Enum.GetValues(typeof(PackageSignatureType)).Cast<PackageSignatureType>().OrderBy(x => x));
        }

        public static IEnumerable<object[]> HasExpectedValuesData => Expected
            .Select(x => new object[] { x.Key, x.Value });
    }
}
