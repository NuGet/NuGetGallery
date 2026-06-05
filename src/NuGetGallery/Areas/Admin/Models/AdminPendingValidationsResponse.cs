// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGetGallery.Areas.Admin.Models
{
    public class AdminPendingValidationsResponse
    {
        public List<AdminPendingValidationResult> Results { get; set; }
    }

    public class AdminPendingValidationResult
    {
        public int PackageKey { get; set; }

        public string PackageId { get; set; }

        public string PackageVersion { get; set; }

        public string ValidatingType { get; set; }

        public List<AdminPendingValidationSetResult> ValidationSets { get; set; }
    }

    public class AdminPendingValidationSetResult
    {
        public long Key { get; set; }

        public Guid ValidationTrackingId { get; set; }

        public string ValidationSetStatus { get; set; }

        public DateTime Created { get; set; }

        public DateTime Updated { get; set; }

        public List<AdminPendingValidationStepResult> Validations { get; set; }
    }

    public class AdminPendingValidationStepResult
    {
        public Guid Key { get; set; }

        public string Type { get; set; }

        public string Status { get; set; }

        public DateTime? Started { get; set; }

        public DateTime ValidationStatusTimestamp { get; set; }
    }
}
