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
        private readonly string _packageUrlTemplate;

        public ValidatorBase(string packageUrlTemplate)
        {
            _packageUrlTemplate = packageUrlTemplate;
        }

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

        protected string GetPackageUrl(PackageValidationMessage message)
        {
            string packageUrl;
            if (message.Package.DownloadUrl != null)
            {
                packageUrl = message.Package.DownloadUrl.AbsoluteUri;
            }
            else
            {
                packageUrl = BuildStorageUrl(message.Package.Id, message.PackageVersion);
            }

            return packageUrl;
        }

        private string BuildStorageUrl(string packageId, string packageVersion)
        {
            // The VCS service needs a blob storage URL, which the NuGet API does not expose.
            // Build one from a template here.
            // Guarantee all URL transformations (such as URL encoding) are performed.
            return new Uri(_packageUrlTemplate
                .Replace("{id}", packageId)
                .Replace("{version}", packageVersion)
                .ToLowerInvariant()).AbsoluteUri;
        }
    }
}