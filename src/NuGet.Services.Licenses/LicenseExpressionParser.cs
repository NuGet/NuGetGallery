// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging.Licenses;

namespace NuGet.Services.Licenses
{
    public class LicenseExpressionParser : ILicenseExpressionParser
    {
        public NuGetLicenseExpression Parse(string licenseExpression)
            => NuGetLicenseExpression.Parse(licenseExpression);
    }
}
