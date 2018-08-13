// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace UpdateLicenseReports
{
    internal class PackageLicenseReport
    {
        public int Sequence { get; set; }

        public string PackageId { get; set; }

        public string Version { get; set; }

        public string ReportUrl { get; set; }

        public string Comment { get; set; }

        public ICollection<string> Licenses { get; private set; }

        public PackageLicenseReport(int sequence)
        {
            Sequence = sequence;
            PackageId = null;
            Version = null;
            ReportUrl = null;
            Comment = null;
            Licenses = new LinkedList<string>();
        }

        public override string ToString()
        {
            return string.Format("{{ {0}, {1}, [ {2} ] }}", Sequence, string.Join(", ", PackageId, Version, ReportUrl, Comment), string.Join(", ", Licenses));
        }
    }
}
