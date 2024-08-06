// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.Licenses
{
    /// <summary>
    /// Higher level wrapper around <see cref="ILicenseExpressionSegmentator"/>
    /// </summary>
    public interface ILicenseExpressionSplitter
    {
        /// <summary>
        /// Single entry point for splitting license expression into highlightable segments.
        /// </summary>
        /// <param name="licenseExpression">License expression to split.</param>
        /// <returns>List of segments of which license expression was comprised.</returns>
        /// <seealso cref="ILicenseExpressionSegmentator"/>
        List<CompositeLicenseExpressionSegment> SplitExpression(string licenseExpression);
    }
}
