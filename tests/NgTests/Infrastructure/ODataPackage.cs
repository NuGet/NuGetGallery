// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NgTests.Infrastructure
{
    public class ODataPackage
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string Hash { get; set; }
        public bool Listed { get; set; }

        public DateTime Created { get; set; }
        public DateTime? LastEdited { get; set; }
        public DateTime Published { get; set; }
        public string LicenseNames { get; set; }
        public string LicenseReportUrl { get; set; }
    }
}