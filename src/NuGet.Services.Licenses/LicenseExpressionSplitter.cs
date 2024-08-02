// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Licenses
{
    public class LicenseExpressionSplitter : ILicenseExpressionSplitter
    {
        private readonly ILicenseExpressionParser _parser;
        private readonly ILicenseExpressionSegmentator _segmentator;

        public LicenseExpressionSplitter(
            ILicenseExpressionParser parser,
            ILicenseExpressionSegmentator segmentator)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _segmentator = segmentator ?? throw new ArgumentNullException(nameof(segmentator));
        }

        public List<CompositeLicenseExpressionSegment> SplitExpression(string licenseExpression)
        {
            if (licenseExpression == null)
            {
                throw new ArgumentNullException(nameof(licenseExpression));
            }

            var expressionRoot = _parser.Parse(licenseExpression);

            var meaningfulSegments = _segmentator.GetLicenseExpressionSegments(expressionRoot);
            return _segmentator.SplitFullExpression(licenseExpression, meaningfulSegments);
        }
    }
}
