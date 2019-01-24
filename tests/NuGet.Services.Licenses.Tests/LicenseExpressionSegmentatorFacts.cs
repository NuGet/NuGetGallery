// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Packaging.Licenses;
using Xunit;

namespace NuGet.Services.Licenses.Tests
{
    public class TheGetLicenseExpressionSegmentsMethod : LicenseExpressionSegmentatorFactsBase
    {
        [Fact]
        public void ThrowsWhenLicenseExpressionRootIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => _target.GetLicenseExpressionSegments(null));
        }

        public static IEnumerable<object[]> LicenseExpressionsAndSegments => new object[][]
        {
            new object[] { "MIT", new[] { License("MIT") } },
            new object[] { "((MIT))", new[] { License("MIT") } },
            new object[] { "MIT+", new[] { License("MIT"), Plus() } },
            new object[] { "(MIT OR ISC)", new[] { License("MIT"), Or(), License("ISC") } },
            new object[] { "(((MIT  OR ISC)))", new[] { License("MIT"), Or(), License("ISC") } },
            new object[] { "(((MIT)) OR  ((ISC)))", new[] { License("MIT"), Or(), License("ISC") } },
            new object[] { "(MIT OR ISC  WITH Classpath-exception-2.0)", new[] { License("MIT"), Or(), License("ISC"), With(), Exception("Classpath-exception-2.0") } },
            new object[] { "(MIT+ OR  ((ISC)))", new[] { License("MIT"), Plus(), Or(), License("ISC") } },
        };

        [Theory]
        [MemberData(nameof(LicenseExpressionsAndSegments))]
        public void ProducesProperSequenceOfSegments(string licenseExpression, CompositeLicenseExpressionSegment[] expectedSequence)
        {
            var expressionTreeRoot = NuGetLicenseExpression.Parse(licenseExpression);

            var segments = _target.GetLicenseExpressionSegments(expressionTreeRoot);

            Assert.NotNull(segments);
            Assert.Equal(expectedSequence, segments, new CompositeLicenseExpressionSegmentEqualityComparer());
        }
    }

    public class TheSplitFullExpressionMethod : LicenseExpressionSegmentatorFactsBase
    {
        [Fact]
        public void ThrowsWhenLicenseExpressionIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => _target.SplitFullExpression(null, new CompositeLicenseExpressionSegment[0]));
            Assert.Equal("licenseExpression", ex.ParamName);
        }

        [Fact]
        public void ThrowsWhenSegmentIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => _target.SplitFullExpression("", null));
            Assert.Equal("segments", ex.ParamName);
        }

        public static IEnumerable<object[]> LicenseExpressionsAndSegments => new object[][]
        {
            new object[] {
                "MIT",
                new[] { License("MIT") },
                new[] { License("MIT") }
            },
            new object[] {
                "(MIT+)",
                new[] { License("MIT"), Plus() },
                new[] { Other("("), License("MIT"), Plus(), Other(")") }
            },
            new object[] {
                "(MIT OR ISC)",
                new[] { License("MIT"), Or(), License("ISC") },
                new[] { Other("("), License("MIT"), Other(" "), Or(), Other(" "), License("ISC"), Other(")") }
            },
            new object[] {
                "(((MIT  OR ISC)))",
                new[] { License("MIT"), Or(), License("ISC") },
                new[] { Other("((("), License("MIT"), Other("  "), Or(), Other(" "), License("ISC"), Other(")))") }
            },
            new object[] {
                "(((MIT)) OR  ((ISC)))",
                new[] { License("MIT"), Or(), License("ISC") },
                new[] { Other("((("), License("MIT"), Other(")) "), Or(), Other("  (("), License("ISC"), Other(")))") }
            },
            new object[] {
                "(MIT OR ISC  WITH Classpath-exception-2.0)",
                new[] { License("MIT"), Or(), License("ISC"), With(), Exception("Classpath-exception-2.0") },
                new[] { Other("("), License("MIT"), Other(" "), Or(), Other(" "), License("ISC"), Other("  "), With(), Other(" "), Exception("Classpath-exception-2.0"), Other(")") }
            },
        };

        [Theory]
        [MemberData(nameof(LicenseExpressionsAndSegments))]
        public void AddsParenthesesAndWhitespace(string licenseExpression, CompositeLicenseExpressionSegment[] segments, CompositeLicenseExpressionSegment[] expectedSegments)
        {
            var result = _target.SplitFullExpression(licenseExpression, segments);

            Assert.Equal(expectedSegments, result, new CompositeLicenseExpressionSegmentEqualityComparer());
        }
    }

    public class LicenseExpressionSegmentatorFactsBase
    {
        protected LicenseExpressionSegmentator _target;

        public LicenseExpressionSegmentatorFactsBase()
        {
            _target = new LicenseExpressionSegmentator();
        }

        protected static CompositeLicenseExpressionSegment License(string licenseId)
            => new CompositeLicenseExpressionSegment(licenseId, CompositeLicenseExpressionSegmentType.LicenseIdentifier);

        protected static CompositeLicenseExpressionSegment Operator(string operatorName)
            => new CompositeLicenseExpressionSegment(operatorName, CompositeLicenseExpressionSegmentType.Operator);

        protected static CompositeLicenseExpressionSegment Exception(string exceptionId)
            => new CompositeLicenseExpressionSegment(exceptionId, CompositeLicenseExpressionSegmentType.ExceptionIdentifier);

        protected static CompositeLicenseExpressionSegment Or() => Operator("OR");
        protected static CompositeLicenseExpressionSegment And() => Operator("AND");
        protected static CompositeLicenseExpressionSegment With() => Operator("WITH");
        protected static CompositeLicenseExpressionSegment Plus() => Operator("+");

        protected static CompositeLicenseExpressionSegment Other(string value)
            => new CompositeLicenseExpressionSegment(value, CompositeLicenseExpressionSegmentType.Other);
    }

    internal class CompositeLicenseExpressionSegmentEqualityComparer : IEqualityComparer<CompositeLicenseExpressionSegment>
    {
        public bool Equals(CompositeLicenseExpressionSegment x, CompositeLicenseExpressionSegment y)
        {
            if (x == null || y == null)
            {
                return x == y;
            }

            return x.Type == y.Type && x.Value == y.Value;
        }

        public int GetHashCode(CompositeLicenseExpressionSegment obj)
        {
            return obj.Type.GetHashCode() ^ obj.Value.GetHashCode();
        }
    }
}
