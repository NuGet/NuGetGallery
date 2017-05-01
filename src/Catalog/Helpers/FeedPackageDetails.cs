// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    public class FeedPackageDetails
    {
        public Uri ContentUri { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastEditedDate { get; set; }
        public DateTime PublishedDate { get; set; }
        public string LicenseNames { get; set; }
        public string LicenseReportUrl { get; set; }
    }
}
