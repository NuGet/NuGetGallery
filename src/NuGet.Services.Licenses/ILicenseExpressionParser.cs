// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging.Licenses;

namespace NuGet.Services.Licenses
{
    public interface ILicenseExpressionParser
    {
        /// <summary>
        /// Parses a license expression into expression tree.
        /// </summary>
        /// <param name="licenseExpression">String representation of the license expression.</param>
        /// <returns>Root node of the expression tree produced by the license expression.</returns>
        NuGetLicenseExpression Parse(string licenseExpression);
    }
}
