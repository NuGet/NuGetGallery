using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery
{
    public class SemVerFacts
    {
        public class TheConstructor
        {
            [Fact]
            public void RequiresNonNegativeMajorVersion()
            {
                ContractAssert.ThrowsOutOfRange(() => new SemVer(-1, 1), Strings.ParameterMustBeNonNegative, "major", -1);
                ContractAssert.ThrowsOutOfRange(() => new SemVer(-1, 1, "tag"), Strings.ParameterMustBeNonNegative, "major", -1);
            }

            [Fact]
            public void RequiresNonNegativeMinorVersion()
            {
                ContractAssert.ThrowsOutOfRange(() => new SemVer(0, -1), Strings.ParameterMustBeNonNegative, "minor", -1);
                ContractAssert.ThrowsOutOfRange(() => new SemVer(0, -1, "tag"), Strings.ParameterMustBeNonNegative, "minor", -1);
            }

            [Fact]
            public void RequiresNonNegativePatchVersion()
            {
                ContractAssert.ThrowsOutOfRange(() => new SemVer(0, 0, -1), Strings.ParameterMustBeNonNegative, "patch", -1);
                ContractAssert.ThrowsOutOfRange(() => new SemVer(0, 0, -1, "tag"), Strings.ParameterMustBeNonNegative, "patch", -1);
            }

            [Fact]
            public void RequiresNonNegativeRevisionVersion()
            {
                ContractAssert.ThrowsOutOfRange(() => new SemVer(0, 0, 0, -1), Strings.ParameterMustBeNonNegative, "revision", -1);
                ContractAssert.ThrowsOutOfRange(() => new SemVer(0, 0, 0, -1, "tag"), Strings.ParameterMustBeNonNegative, "revision", -1);
            }

            [Fact]
            public void InitializesUnspecifiedPatchToZero()
            {
                Assert.Equal(0, new SemVer(1, 1).Patch);
            }

            [Fact]
            public void InitializesUnspecifiedRevisionToZero()
            {
                Assert.Equal(0, new SemVer(1, 1, 1).Revision);
            }

            [Fact]
            public void InitializesUnspecifiedTagToNull()
            {
                Assert.Null(new SemVer(1, 1).Tag);
                Assert.Null(new SemVer(1, 1, 1).Tag);
                Assert.Null(new SemVer(1, 1, 1, 1).Tag);
            }

            [Fact]
            public void NormalizesEmptyStringToNull()
            {
                Assert.Null(new SemVer(1, 1, String.Empty).Tag);
                Assert.Null(new SemVer(1, 1, 1, String.Empty).Tag);
                Assert.Null(new SemVer(1, 1, 1, 1, String.Empty).Tag);
            }
        }

        public class TheToStringMethod
        {
            [Theory]
            [InlineData(1, 0, 0, 0, null, "1.0.0")]
            [InlineData(1, 1, 0, 0, null, "1.1.0")]
            [InlineData(1, 1, 1, 0, null, "1.1.1")]
            [InlineData(1, 1, 1, 1, null, "1.1.1.1")]
            [InlineData(1, 0, 0, 0, "tag", "1.0.0-tag")]
            [InlineData(1, 1, 0, 0, "tag", "1.1.0-tag")]
            [InlineData(1, 1, 1, 0, "tag", "1.1.1-tag")]
            [InlineData(1, 1, 1, 1, "tag", "1.1.1.1-tag")]
            public void NormalizesStringOutputForDisplayAndUniqueness(int major, int minor, int patch, int revision, string tag, string expectedString)
            {
                Assert.Equal(expectedString, new SemVer(major, minor, patch, revision, tag).ToString(), StringComparer.Ordinal);
            }
        }

        public class TheParseMethod
        {
            [Theory]
            [InlineData("1.0", 1, 0, 0, 0, null)]
            [InlineData("1.1", 1, 1, 0, 0, null)]
            [InlineData("1.1.0", 1, 1, 0, 0, null)]
            [InlineData("1.1.1", 1, 1, 1, 0, null)]
            [InlineData("1.1.1.0", 1, 1, 1, 0, null)]
            [InlineData("1.1.1.1", 1, 1, 1, 1, null)]
            [InlineData("1.0-tag", 1, 0, 0, 0, "tag")]
            [InlineData("1.1-tag", 1, 1, 0, 0, "tag")]
            [InlineData("1.1.0-tag", 1, 1, 0, 0, "tag")]
            [InlineData("1.1.1-tag", 1, 1, 1, 0, "tag")]
            [InlineData("1.1.1.0-tag", 1, 1, 1, 0, "tag")]
            [InlineData("1.1.1.1-tag", 1, 1, 1, 1, "tag")]
            public void CorrectlyParsesValidSemVerStrings(string input, int major, int minor, int patch, int revision, string tag)
            {
                var ver = SemVer.Parse(input);
                Assert.Equal(major, ver.Major);
                Assert.Equal(minor, ver.Minor);
                Assert.Equal(patch, ver.Patch);
                Assert.Equal(revision, ver.Revision);
                Assert.Equal(tag, ver.Tag);
            }

            [Theory]
            [InlineData("not even close")]
            [InlineData("a.b.c.d-e")]
            [InlineData("a.b.c-e")]
            [InlineData("a.b-e")]
            [InlineData("a.b.c.d")]
            [InlineData("a.b.c")]
            [InlineData("a.b")]
            [InlineData("1.2.closer")]
            [InlineData("1.2-notquite?")]
            [InlineData("1.2.0.0.0.0.0.0.0.0.0-ILOVEZEROS")]
            [InlineData("1.2.0-build+isnotsupportedyet")]
            [InlineData(" 1.2-sorrynospaces")]
            [InlineData("1.2-trimityourself ")]
            public void ThrowsFormatExceptionOnInvalidStrings(string input)
            {
                var ex = Assert.Throws<FormatException>(() => SemVer.Parse(input));
                Assert.Equal(String.Format(Strings.InvalidSemanticVersion, input), ex.Message);
            }
        }

        public class TheTryParseMethod
        {
            [Theory]
            [InlineData("1.0", 1, 0, 0, 0, null)]
            [InlineData("1.1", 1, 1, 0, 0, null)]
            [InlineData("1.1.0", 1, 1, 0, 0, null)]
            [InlineData("1.1.1", 1, 1, 1, 0, null)]
            [InlineData("1.1.1.0", 1, 1, 1, 0, null)]
            [InlineData("1.1.1.1", 1, 1, 1, 1, null)]
            [InlineData("1.0-tag", 1, 0, 0, 0, "tag")]
            [InlineData("1.1-tag", 1, 1, 0, 0, "tag")]
            [InlineData("1.1.0-tag", 1, 1, 0, 0, "tag")]
            [InlineData("1.1.1-tag", 1, 1, 1, 0, "tag")]
            [InlineData("1.1.1.0-tag", 1, 1, 1, 0, "tag")]
            [InlineData("1.1.1.1-tag", 1, 1, 1, 1, "tag")]
            public void CorrectlyParsesValidSemVerStrings(string input, int major, int minor, int patch, int revision, string tag)
            {
                SemVer ver;
                Assert.True(SemVer.TryParse(input, out ver));
                Assert.Equal(major, ver.Major);
                Assert.Equal(minor, ver.Minor);
                Assert.Equal(patch, ver.Patch);
                Assert.Equal(revision, ver.Revision);
                Assert.Equal(tag, ver.Tag);
            }

            [Theory]
            [InlineData("not even close")]
            [InlineData("a.b.c.d-e")]
            [InlineData("a.b.c-e")]
            [InlineData("a.b-e")]
            [InlineData("a.b.c.d")]
            [InlineData("a.b.c")]
            [InlineData("a.b")]
            [InlineData("1.2.closer")]
            [InlineData("1.2-notquite?")]
            [InlineData("1.2.0.0.0.0.0.0.0.0.0-ILOVEZEROS")]
            [InlineData("1.2.0-build+isnotsupportedyet")]
            [InlineData(" 1.2-sorrynospaces")]
            [InlineData("1.2-trimityourself ")]
            public void ReturnsFalseOnInvalidStrings(string input)
            {
                SemVer ver;
                Assert.False(SemVer.TryParse(input, out ver));
                Assert.Equal(0, ver.Major);
                Assert.Equal(0, ver.Minor);
                Assert.Equal(0, ver.Patch);
                Assert.Equal(0, ver.Revision);
                Assert.Null(ver.Tag);
            }
        }

        public class TheEqualsMethod
        {
            public static IEnumerable<object[]> EqualsData
            {
                get
                {
                    yield return new object[] { new SemVer(1, 0), new SemVer(1, 0) };
                    yield return new object[] { new SemVer(1, 0), new SemVer(1, 0, 0) };
                    yield return new object[] { new SemVer(1, 0), new SemVer(1, 0, 0, 0) };
                    yield return new object[] { new SemVer(1, 0), new SemVer(1, 0, 0, 0, null) };
                    yield return new object[] { new SemVer(1, 0, "tag"), new SemVer(1, 0, "tag") };
                    yield return new object[] { new SemVer(1, 0, "tag"), new SemVer(1, 0, 0, "tag") };
                    yield return new object[] { new SemVer(1, 0, "tag"), new SemVer(1, 0, 0, 0, "tag") };
                    yield return new object[] { new SemVer(1, 1, 2, 3), new SemVer(1, 1, 2, 3) };
                    yield return new object[] { new SemVer(1, 1, 2, 3, "tag"), new SemVer(1, 1, 2, 3, "tag") };
                }
            }

            public static IEnumerable<object[]> NotEqualsData
            {
                get
                {
                    yield return new object[] { new SemVer(1, 0), new SemVer(1, 1) };
                    yield return new object[] { new SemVer(1, 0), new SemVer(1, 1, 0) };
                    yield return new object[] { new SemVer(1, 0), new SemVer(1, 1, 0, 0) };
                    yield return new object[] { new SemVer(1, 0), new SemVer(1, 1, 0, 0, null) };
                    yield return new object[] { new SemVer(1, 0, "tag"), new SemVer(1, 0, "Tag") };
                    yield return new object[] { new SemVer(1, 0, "tag"), new SemVer(1, 0, 0, "Tag") };
                    yield return new object[] { new SemVer(1, 0, "tag"), new SemVer(1, 0, 0, 0, "Tag") };
                    yield return new object[] { new SemVer(1, 1, 3, 2), new SemVer(1, 1, 2, 3) };
                    yield return new object[] { new SemVer(1, 1, 2, 3, "tag"), new SemVer(1, 1, 2, 3, "Tag") };
                }
            }

            [Theory]
            [PropertyData("EqualsData")]
            public void ReturnsTrueWhenVersionsAreEqual(SemVer left, SemVer right)
            {
                Assert.Equal(left, right);
            }

            [Theory]
            [PropertyData("NotEqualsData")]
            public void ReturnsFalseWhenVersionsAreNotEqual(SemVer left, SemVer right)
            {
                Assert.NotEqual(left, right);
            }
        }

        public class TheCompareToMethod
        {
            public static IEnumerable<object[]> ComparisonData
            {
                get
                {
                    yield return new object[] { new SemVer(1, 0), new SemVer(1, 1) };
                    yield return new object[] { new SemVer(1, 0, 0), new SemVer(1, 1) };
                    yield return new object[] { new SemVer(1, 0, 0, 0), new SemVer(1, 1) };
                    yield return new object[] { new SemVer(1, 0, 0, 0, "tag"), new SemVer(1, 1) };
                    yield return new object[] { new SemVer(1, 9), new SemVer(2, 0) };
                    yield return new object[] { new SemVer(1, 9, 9), new SemVer(2, 0) };
                    yield return new object[] { new SemVer(1, 9, 9, 9), new SemVer(2, 0) };
                    yield return new object[] { new SemVer(1, 9, 9, 9, "tag"), new SemVer(2, 0) };
                    yield return new object[] { new SemVer(1, 9), new SemVer(1, 10) };
                    yield return new object[] { new SemVer(1, 0, 9), new SemVer(1, 0, 10) };
                    yield return new object[] { new SemVer(1, 0, 0, 9), new SemVer(1, 0, 0, 10) };
                    yield return new object[] { new SemVer(1, 0, "alpha"), new SemVer(1, 0, "beta") };
                    yield return new object[] { new SemVer(1, 0, "beta"), new SemVer(1, 0) };
                    yield return new object[] { new SemVer(1, 0, 0, "alpha"), new SemVer(1, 0, 0, "beta") };
                    yield return new object[] { new SemVer(1, 0, 0, "beta"), new SemVer(1, 0, 0) };
                    yield return new object[] { new SemVer(1, 0, 0, 0, "alpha"), new SemVer(1, 0, 0, 0, "beta") };
                    yield return new object[] { new SemVer(1, 0, 0, 0, "beta"), new SemVer(1, 0, 0, 0) };
                    yield return new object[] { new SemVer(1, 0, 0, 0, "alpha.a"), new SemVer(1, 0, 0, 0, "alpha.b") };
                    yield return new object[] { new SemVer(1, 0, 0, 0, "alpha"), new SemVer(1, 0, 0, 0, "alpha.a") };
                    yield return new object[] { new SemVer(1, 0, 0, 0, "alpha.9"), new SemVer(1, 0, 0, 0, "alpha.10") };
                    yield return new object[] { new SemVer(1, 0, 0, 0, "alpha.9.a"), new SemVer(1, 0, 0, 0, "alpha.10") };
                    yield return new object[] { new SemVer(1, 0, 0, 0, "alpha.9.a"), new SemVer(1, 0, 0, 0, "alpha.9.b") };
                    yield return new object[] { new SemVer(1, 0, 0, 0, "alpha.1"), new SemVer(1, 0, 0, 0, "alpha.a") };
                    yield return new object[] { new SemVer(1, 0, 0, 0, "alpha"), new SemVer(1, 0, 0, 0, "alpha.1") };
                    yield return new object[] { new SemVer(1, 0, 0, 0, "alpha"), new SemVer(1, 0, 0, 0, "alpha.a") };
                }
            }

            public static IEnumerable<object[]> EqualityData
            {
                get
                {
                    yield return new object[] { new SemVer(1, 0), new SemVer(1, 0) };
                    yield return new object[] { new SemVer(1, 0), new SemVer(1, 0, 0) };
                    yield return new object[] { new SemVer(1, 0), new SemVer(1, 0, 0, 0) };
                    yield return new object[] { new SemVer(1, 0), new SemVer(1, 0, 0, 0, null) };
                    yield return new object[] { new SemVer(1, 0, "tag"), new SemVer(1, 0, "tag") };
                    yield return new object[] { new SemVer(1, 0, "tag"), new SemVer(1, 0, 0, "tag") };
                    yield return new object[] { new SemVer(1, 0, "tag"), new SemVer(1, 0, 0, 0, "tag") };
                    yield return new object[] { new SemVer(1, 1, 2, 3), new SemVer(1, 1, 2, 3) };
                    yield return new object[] { new SemVer(1, 1, 2, 3, "tag"), new SemVer(1, 1, 2, 3, "tag") };
                }
            }

            [Theory]
            [PropertyData("ComparisonData")]
            public void CorrectlyEvaluatesComparisons(SemVer smaller, SemVer bigger)
            {
                Assert.Equal(1, bigger.CompareTo(smaller));
                Assert.Equal(-1, smaller.CompareTo(bigger));
            }

            [Theory]
            [PropertyData("EqualityData")]
            public void CorrectlyEvaluatesEquality(SemVer left, SemVer right)
            {
                Assert.Equal(0, right.CompareTo(left));
                Assert.Equal(0, left.CompareTo(right));
            }
        }

        public class TheFromSemanticVersionMethod
        {
            public static IEnumerable<object[]> Data
            {
                get
                {
                    yield return new object[] { new SemanticVersion("1.0"), new SemVer(1, 0) };
                    yield return new object[] { new SemanticVersion("1.2"), new SemVer(1, 2) };
                    yield return new object[] { new SemanticVersion("1.2-alpha"), new SemVer(1, 2, "alpha") };
                    yield return new object[] { new SemanticVersion("1.2.0"), new SemVer(1, 2, 0) };
                    yield return new object[] { new SemanticVersion("1.2.3"), new SemVer(1, 2, 3) };
                    yield return new object[] { new SemanticVersion("1.2.3-alpha"), new SemVer(1, 2, 3, "alpha") };
                    yield return new object[] { new SemanticVersion("1.2.3.0"), new SemVer(1, 2, 3) };
                    yield return new object[] { new SemanticVersion("1.2.3.4"), new SemVer(1, 2, 3, 4) };
                    yield return new object[] { new SemanticVersion("1.2.3.4-alpha"), new SemVer(1, 2, 3, 4, "alpha") };
                    yield return new object[] { new SemanticVersion(new Version(1, 2)), new SemVer(1, 2) };
                    yield return new object[] { new SemanticVersion(new Version(1, 2, 3)), new SemVer(1, 2, 3) };
                    yield return new object[] { new SemanticVersion(new Version(1, 2, 3, 4)), new SemVer(1, 2, 3, 4) };
                    yield return new object[] { new SemanticVersion(new Version(1, 2), "alpha"), new SemVer(1, 2, "alpha") };
                    yield return new object[] { new SemanticVersion(new Version(1, 2, 3), "alpha"), new SemVer(1, 2, 3, "alpha") };
                    yield return new object[] { new SemanticVersion(new Version(1, 2, 3, 4), "alpha"), new SemVer(1, 2, 3, 4, "alpha") };
                    yield return new object[] { new SemanticVersion(1, 2, 3, "alpha"), new SemVer(1, 2, 3, "alpha") };
                    yield return new object[] { new SemanticVersion(1, 2, 3, 4), new SemVer(1, 2, 3, 4) };
                }
            }

            [Theory]
            [PropertyData("Data")]
            public void CorrectlyConvertsVersion(SemanticVersion nugetSemVer, SemVer expected)
            {
                Assert.Equal(expected, SemVer.FromSemanticVersion(nugetSemVer));
            }
        }
    }
}
