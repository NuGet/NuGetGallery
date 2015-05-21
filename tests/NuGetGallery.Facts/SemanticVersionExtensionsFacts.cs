// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet;
using Xunit;

namespace NuGetGallery
{
    public class SemanticVersionExtensionsFacts
    {
        public class TheToNormalizedStringMethod
        {
            public static IEnumerable<object[]> Data
            {
                get
                {
                    yield return new object[] { new SemanticVersion("1.0"), "1.0.0" };
                    yield return new object[] { new SemanticVersion("1.0.0"), "1.0.0" };
                    yield return new object[] { new SemanticVersion("1.0.0.0"), "1.0.0" };
                    yield return new object[] { new SemanticVersion("1.2"), "1.2.0" };
                    yield return new object[] { new SemanticVersion("1.2-alpha"), "1.2.0-alpha" };
                    yield return new object[] { new SemanticVersion("1.2.0"), "1.2.0" };
                    yield return new object[] { new SemanticVersion("1.2.3"), "1.2.3" };
                    yield return new object[] { new SemanticVersion("1.2.3-alpha"), "1.2.3-alpha" };
                    yield return new object[] { new SemanticVersion("1.2.3.0"), "1.2.3" };
                    yield return new object[] { new SemanticVersion("1.2.3.4"), "1.2.3.4" };
                    yield return new object[] { new SemanticVersion("1.2.3.4-alpha"), "1.2.3.4-alpha" };
                    yield return new object[] { new SemanticVersion(new Version(1, 2)), "1.2.0" };
                    yield return new object[] { new SemanticVersion(new Version(1, 2, 3)), "1.2.3" };
                    yield return new object[] { new SemanticVersion(new Version(1, 2, 3, 4)), "1.2.3.4" };
                    yield return new object[] { new SemanticVersion(new Version(1, 2), "alpha"), "1.2.0-alpha" };
                    yield return new object[] { new SemanticVersion(new Version(1, 2, 3), "alpha"), "1.2.3-alpha" };
                    yield return new object[] { new SemanticVersion(new Version(1, 2, 3, 4), "alpha"), "1.2.3.4-alpha" };
                    yield return new object[] { new SemanticVersion(1, 2, 3, "alpha"), "1.2.3-alpha" };
                    yield return new object[] { new SemanticVersion(1, 2, 3, 4), "1.2.3.4" };
                    yield return new object[] { new SemanticVersion("010.020.030.040"), "10.20.30.40" };
                    yield return new object[] { new SemanticVersion("01.02.03.04"), "1.2.3.4" };
                    yield return new object[] { new SemanticVersion("010.020.030-alpha"), "10.20.30-alpha" };
                    yield return new object[] { new SemanticVersion("01.02.03-alpha"), "1.2.3-alpha" };
                    yield return new object[] { new SemanticVersion("010.020-alpha"), "10.20.0-alpha" };
                    yield return new object[] { new SemanticVersion("01.02.0-alpha"), "1.2.0-alpha" };
                }
            }

            [Theory]
            [MemberData("Data")]
            public void NormalizesStringOutputForDisplayAndUniqueness(SemanticVersion version, string expected)
            {
                Assert.Equal(expected, version.ToNormalizedString(), StringComparer.Ordinal);
            }
        }
    }
}
