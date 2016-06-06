// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Validation.Common
{
    public class PackageValidationAuditEntry
    {
        public string ValidatorName { get; set; }
        public string Message { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }
}