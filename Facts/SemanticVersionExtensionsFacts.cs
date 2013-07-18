﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet;
using Xunit;
using Xunit.Extensions;

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
                }
            }

            [Theory]
            [PropertyData("Data")]
            public void NormalizesStringOutputForDisplayAndUniqueness(SemanticVersion version, string expected)
            {
                Assert.Equal(expected, version.ToNormalizedString(), StringComparer.Ordinal);
            }
        }
    }
}
