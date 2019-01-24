// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Licenses
{
    /// <summary>
    /// Used to specify type of the Segment for composte license expression display
    /// </summary>
    public enum CompositeLicenseExpressionSegmentType
    {
        /// <summary>
        /// Catch-all type for all that don't have otherwise specific type
        /// (used mostly for parentheses)
        /// </summary>
        Other = 0,

        /// <summary>
        /// License identifier
        /// </summary>
        LicenseIdentifier = 1,

        /// <summary>
        /// License exception identifier
        /// </summary>
        ExceptionIdentifier = 2,

        /// <summary>
        /// License expression operator
        /// </summary>
        Operator = 3
    }
}