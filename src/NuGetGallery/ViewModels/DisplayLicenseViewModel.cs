// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGet.Services.Licenses;

namespace NuGetGallery
{
    public class DisplayLicenseViewModel : PackageViewModel
    {
        public EmbeddedLicenseFileType EmbeddedLicenseType { get; set; }
        public string LicenseExpression { get; set; }
        public string LicenseUrl { get; set; }
        public IReadOnlyCollection<string> LicenseNames { get; set; }
        public IReadOnlyCollection<CompositeLicenseExpressionSegment> LicenseExpressionSegments { get; set; }
        public string LicenseFileContents { get; set; }
        public string LicenseFileContentsMd { get; set; }
    }
}