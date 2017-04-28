// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation.Common.Validators
{
    public abstract class ValidatorBase
        : IValidator
    {
        public abstract string Name { get; }

        public virtual TimeSpan VisibilityTimeout
        {
            get
            {
                return TimeSpan.FromMinutes(5);
            }
        }

        public abstract Task<ValidationResult> ValidateAsync(PackageValidationMessage message, List<PackageValidationAuditEntry> auditEntries);

        protected void WriteAuditEntry(List<PackageValidationAuditEntry> auditEntries, string message, ValidationEvent validationEvent)
        {
            auditEntries.Add(new PackageValidationAuditEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                ValidatorName = Name,
                Message = message,
                EventId = validationEvent,
            });
        }
    }
}