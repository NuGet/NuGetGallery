// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.Helpers
{
    public static class LicenseExpressionRedirectUrlHelper
    {
        public const string LicenseExpressionHostname = "licenses.nuget.org";
        public const string LicenseExpressionDeprecationUrlFormat = "https://" + LicenseExpressionHostname + "/{0}";

        public static string GetLicenseExpressionRedirectUrl(string licenseExpression)
        {
            return new Uri(string.Format(LicenseExpressionDeprecationUrlFormat, licenseExpression)).AbsoluteUri;
        }
    }
}