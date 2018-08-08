// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace UpdateLicenseReports
{
    public class UpdateLicenseReportsConfiguration
    {
        public string LicenseReportService { get; set; }

        public string LicenseReportUser { get; set; }

        public string LicenseReportPassword { get; set; }

        public int? RetryCount { get; set; }

        public bool Test { get; set; }
    }
}
