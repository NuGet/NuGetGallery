// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Metadata.Catalog.Registration;
using Xunit;

namespace CatalogTests.Registration
{
    public class RegistrationEntryKeyTests
    {
        [Theory]
        [MemberData(nameof(RegistrationEntryKeys))]
        public void EqualsIgnoresCaseAndComparesNormalized(string idA, string versionA, string idB, string versionB, bool expectedEquals)
        {
            // Arrange
            var a = new RegistrationEntryKey(new RegistrationKey(idA), versionA);
            var b = new RegistrationEntryKey(new RegistrationKey(idB), versionB);

            // Act
            var actualEqualsA = a.Equals(b);
            var actualEqualsB = b.Equals(a);

            // Assert
            Assert.Equal(expectedEquals, actualEqualsA);
            Assert.Equal(expectedEquals, actualEqualsB);
        }

        [Theory]
        [MemberData(nameof(EqualRegistrationEntryKeys))]
        public void GetHashCodeIgnoresCaseAndUsesNormalized(string idA, string versionA, string idB, string versionB)
        {
            // Arrange
            var a = new RegistrationEntryKey(new RegistrationKey(idA), versionA);
            var b = new RegistrationEntryKey(new RegistrationKey(idB), versionB);

            // Act
            var hashCodeA = a.GetHashCode();
            var hashCodeB = b.GetHashCode();

            // Assert
            Assert.Equal(hashCodeA, hashCodeB);
        }

        public static IEnumerable<object[]> RegistrationEntryKeys
        {
            get
            {
                yield return new object[] { "NuGet.Core", "2.12.0", "NuGet.Core", "2.12.0", true };
                yield return new object[] { "nuget.core", "2.12.0", "NuGet.Core", "2.12.0", true };
                yield return new object[] { "NuGet.Core", "2.12.0-alpha", "NuGet.Core", "2.12.0-ALPHA", true };
                yield return new object[] { "NuGet.Core", "2.12.0+a", "NuGet.Core", "2.12.0+b", true };
                yield return new object[] { "NuGet.Core", "2.12.0+a", "NuGet.Core", "2.12.0", true };
                yield return new object[] { "NuGet.Core", "2.12.0.0", "NuGet.Core", "2.12.0", true };
                yield return new object[] { "NuGet.Core", "2.012.0", "NuGet.Core", "2.12.0", true };
                yield return new object[] { "NuGet.Versioning", "2.12.0", "NuGet.Core", "2.12.0", false };
                yield return new object[] { "NuGet.Core", "2.13.0", "NuGet.Core", "2.12.0", false };
                yield return new object[] { "NuGet.Core", "2.12.0-alpha", "NuGet.Core", "2.12.0", false };
            }
        }

        public static IEnumerable<object[]> EqualRegistrationEntryKeys =>
            RegistrationEntryKeys
                .Where(a => (bool)a[4])
                .Select(a => new object[] { a[0], a[1], a[2], a[3] });
    }
}
