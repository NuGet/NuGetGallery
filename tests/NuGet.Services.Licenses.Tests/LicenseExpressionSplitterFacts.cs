// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using NuGet.Packaging.Licenses;
using Xunit;

namespace NuGet.Services.Licenses.Tests
{
    public class LicenseExpressionSplitterFacts
    {
        public class TheConstructor : Base
        {
            [Fact]
            public void ConstructorThrowsWhenParserIsNull()
            {
                var ex = Assert.Throws<ArgumentNullException>(() => new LicenseExpressionSplitter(parser: null, segmentator: _segmentatorMock.Object));
                Assert.Equal("parser", ex.ParamName);
            }

            [Fact]
            public void ConstructorThrowsWhenSegmentatorIsNull()
            {
                var ex = Assert.Throws<ArgumentNullException>(() => new LicenseExpressionSplitter(parser: _parserMock.Object, segmentator: null));
                Assert.Equal("segmentator", ex.ParamName);
            }
        }

        public class TheSplitExpressionMethod : Base
        {
            [Fact]
            public void SplitExpressionThrowsWhenLicenseExpressionIsNull()
            {
                var ex = Assert.Throws<ArgumentNullException>(() => _target.SplitExpression(licenseExpression: null));
                Assert.Equal("licenseExpression", ex.ParamName);
            }

            [Fact]
            public void SplitExpressionParsesExpression()
            {
                const string licenseExpression = "some license expression";

                _parserMock
                    .Setup(p => p.Parse(licenseExpression))
                    .Verifiable();

                _target.SplitExpression(licenseExpression);

                _parserMock.Verify();
            }

            [Fact]
            public void SplitExpressionCallsSegmentator()
            {
                const string licenseExpression = "some license expression";
                var expressionRoot = NuGetLicenseExpression.Parse("MIT");

                _parserMock
                    .Setup(p => p.Parse(licenseExpression))
                    .Returns(expressionRoot);

                var segments = new List<CompositeLicenseExpressionSegment>();

                _segmentatorMock
                    .Setup(s => s.GetLicenseExpressionSegments(expressionRoot))
                    .Returns(segments)
                    .Verifiable();

                var expectedResult = new List<CompositeLicenseExpressionSegment>();

                _segmentatorMock
                    .Setup(s => s.SplitFullExpression(licenseExpression, segments))
                    .Returns(expectedResult)
                    .Verifiable();

                var actualResult = _target.SplitExpression(licenseExpression);

                _segmentatorMock.Verify();
                Assert.Same(expectedResult, actualResult);
            }
        }

        public class Base
        {
            protected Mock<ILicenseExpressionParser> _parserMock;
            protected Mock<ILicenseExpressionSegmentator> _segmentatorMock;
            protected LicenseExpressionSplitter _target;

            public Base()
            {
                _parserMock = new Mock<ILicenseExpressionParser>();
                _segmentatorMock = new Mock<ILicenseExpressionSegmentator>();

                _target = new LicenseExpressionSplitter(_parserMock.Object, _segmentatorMock.Object);
            }
        }
    }
}
