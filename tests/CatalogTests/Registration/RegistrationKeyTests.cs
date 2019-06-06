// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Metadata.Catalog.Registration;
using Xunit;

namespace CatalogTests.Registration
{
    public class RegistrationKeyTests
    {
        [Fact]
        public void Constructor_WhenIdIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new RegistrationKey(id: null));

            Assert.Equal("id", exception.ParamName);
        }

        [Theory]
        [MemberData(nameof(RegistrationKeys))]
        public void EqualsIgnoresCase(string idA, string idB, bool expectedEquals)
        {
            // Arrange
            var a = new RegistrationKey(idA);
            var b = new RegistrationKey(idB);

            // Act
            var actualEqualsA = a.Equals(b);
            var actualEqualsB = b.Equals(a);

            // Assert
            Assert.Equal(expectedEquals, actualEqualsA);
            Assert.Equal(expectedEquals, actualEqualsB);
        }

        [Theory]
        [MemberData(nameof(EqualRegistrationKeys))]
        public void GetHashCodeIgnoresCaseAndUsesNormalized(string idA, string idB)
        {
            // Arrange
            var a = new RegistrationKey(idA);
            var b = new RegistrationKey(idB);

            // Act
            var hashCodeA = a.GetHashCode();
            var hashCodeB = b.GetHashCode();

            // Assert
            Assert.Equal(hashCodeA, hashCodeB);
        }

        [Theory]
        [InlineData("a")]
        [InlineData("A")]
        public void ToString_Always_ReturnsLowerCase(string id)
        {
            var key = new RegistrationKey(id);

            Assert.Equal(id.ToLowerInvariant(), key.ToString());
        }

        public static IEnumerable<object[]> RegistrationKeys
        {
            get
            {
                yield return new object[] { "NuGet.Core", "NuGet.Core", true };
                yield return new object[] { "nuget.core", "NuGet.Core", true };
                yield return new object[] { "NuGet.Versioning", "NuGet.Core", false };
            }
        }

        public static IEnumerable<object[]> EqualRegistrationKeys =>
            RegistrationKeys
                .Where(a => (bool)a[2])
                .Select(a => new object[] { a[0], a[1] });
    }
}