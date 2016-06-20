// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using NuGet.Jobs.Validation.Common.OData;

namespace NuGet.Jobs.Validation.Common
{
    public class PackageValidationAudit
    {
        public PackageValidationAudit()
        {
            Entries = new List<PackageValidationAuditEntry>();
        }

        public Guid ValidationId { get; set; }
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public string[] Validators { get; set; }
        public DateTimeOffset Started { get; set; }
        public DateTimeOffset? Completed { get; set; }

        public NuGetPackage Package { get; set; }

        public List<PackageValidationAuditEntry> Entries { get; set; }

        public string Humanize()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}