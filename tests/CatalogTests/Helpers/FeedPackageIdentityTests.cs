// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Versioning;
using Xunit;

namespace CatalogTests.Helpers
{
    public class FeedPackageIdentityTests
    {
        [Flags]
        private enum Equals_Data_States
        {
            IdIsWrong = 1,
            IdIsDifferentCase = 2,
            VersionIsWrong = 4,
            VersionIsDifferentCase = 8
        }

        public static IEnumerable<object[]> Equals_Data
        {
            get
            {
                const string idA = "a";
                const string versionA = "1.0.0-ab";

                for (int i = 0; i < (int)Enum.GetValues(typeof(Equals_Data_States)).Cast<int>().Max() * 2; i++)
                {
                    var idB = (i & (int)Equals_Data_States.IdIsWrong) == 0 ? "a" : "b";

                    if ((i & (int)Equals_Data_States.IdIsDifferentCase) != 0)
                    {
                        idB = idB.ToUpperInvariant();
                    }
                    
                    var versionB = (i & (int)Equals_Data_States.VersionIsWrong) == 0 ? "1.0.0-ab" : "1.0.1-ab";

                    if ((i & (int)Equals_Data_States.VersionIsDifferentCase) != 0)
                    {
                        versionB = versionB.ToUpperInvariant();
                    }

                    var success = (i & (int)Equals_Data_States.IdIsWrong) == 0 && (i & (int)Equals_Data_States.VersionIsWrong) == 0;

                    yield return new object[] { idA, versionA, idB, versionB, success };
                }
            }
        }

        [Theory]
        [MemberData(nameof(Equals_Data))]
        public void FeedPackageIdentityEquals(string idA, string versionA, string idB, string versionB, bool success)
        {
            var packageA = new FeedPackageIdentity(idA, versionA);
            var packageB = new FeedPackageIdentity(idB, versionB);

            Assert.Equal(success, packageA.Equals(packageB));
            Assert.Equal(success, packageB.Equals(packageA));

            Assert.Equal(success, packageA.GetHashCode() == packageB.GetHashCode());
        }

        [Fact]
        public void PackageIdentityConstructorUsesFullString()
        {
            const string id = "id";
            const string versionString = "1.0.0+buildmetadata";
            var package = new PackageIdentity(id, NuGetVersion.Parse(versionString));

            var feedPackage = new FeedPackageIdentity(package);
            var equivalentFeedPackage1 = new FeedPackageIdentity(id, versionString);
            var equivalentFeedPackage2 = new FeedPackageIdentity(package.Id, package.Version.ToFullString());
            var differentFeedPackage = new FeedPackageIdentity(package.Id, package.Version.ToNormalizedString());

            foreach (var equivalentFeedPackage in new FeedPackageIdentity[] { equivalentFeedPackage1, equivalentFeedPackage2 })
            {
                Assert.True(feedPackage.Equals(equivalentFeedPackage));
                Assert.Equal(feedPackage.GetHashCode(), equivalentFeedPackage.GetHashCode());
            }

            Assert.False(feedPackage.Equals(differentFeedPackage));
            Assert.NotEqual(feedPackage.GetHashCode(), differentFeedPackage.GetHashCode());
        }
    }
}
