// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGet.Services.Licenses;

namespace NuGetGallery
{
    public class DisplayLicenseViewModel : PackageViewModel
    {
        public DisplayLicenseViewModel(Package package)
            : base(package)
        {
            EmbeddedLicenseType = package.EmbeddedLicenseType;
            LicenseExpression = package.LicenseExpression;
            if (PackageHelper.TryPrepareUrlForRendering(package.LicenseUrl, out string licenseUrl))
            {
                LicenseUrl = licenseUrl;

                var licenseNames = package.LicenseNames;
                if (!string.IsNullOrEmpty(licenseNames))
                {
                    LicenseNames = licenseNames.Split(',').Select(l => l.Trim());
                }
            }
        }

        public EmbeddedLicenseFileType EmbeddedLicenseType { get; }
        public string LicenseExpression { get; }
        public string LicenseUrl { get; }
        public IEnumerable<string> LicenseNames { get; }
        public IReadOnlyCollection<CompositeLicenseExpressionSegment> LicenseExpressionSegments { get; set; }
        public string LicenseFileContents { get; set; }
    }
}