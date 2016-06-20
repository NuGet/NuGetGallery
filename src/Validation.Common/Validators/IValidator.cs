// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation.Common.Validators
{
    public interface IValidator
    {
        string Name { get; }
        TimeSpan VisibilityTimeout { get; }
        Task<ValidationResult> ValidateAsync(PackageValidationMessage message, List<PackageValidationAuditEntry> auditEntries);
    }
}