// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Licenses
{
    /// <summary>
    /// Represents a portion of a composite license expression allowing to specify its type.
    /// </summary>
    public class CompositeLicenseExpressionSegment
    {
        public CompositeLicenseExpressionSegment(string textValue, CompositeLicenseExpressionSegmentType type)
        {
            Value = textValue ?? throw new ArgumentNullException(nameof(textValue));
            Type = type;
        }

        /// <summary>
        /// Text value of the license expression portion
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Type of the license expression option
        /// </summary>
        public CompositeLicenseExpressionSegmentType Type { get; }
    }
}