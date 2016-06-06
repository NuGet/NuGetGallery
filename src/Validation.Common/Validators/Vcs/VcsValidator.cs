// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.VirusScanning.Vcs;

namespace NuGet.Jobs.Validation.Common.Validators.Vcs
{
    public class VcsValidator
        : ValidatorBase, IValidator
    {
        public const string ValidatorName = "validator-vcs";

        private readonly string _packageUrlTemplate;
        private readonly Uri _callbackUrl;
        private readonly VcsVirusScanningService _scanningService;

        public VcsValidator(string serviceUrl, string callbackUrl, string submitterAlias, string packageUrlTemplate)
        {
            _packageUrlTemplate = packageUrlTemplate;
            _scanningService = new VcsVirusScanningService(new Uri(serviceUrl), "DIRECT", submitterAlias, submitterAlias);
            _callbackUrl = new Uri(callbackUrl);
        }

        public override string Name
        {
            get
            {
                return ValidatorName;
            }
        }

        public override async Task<ValidationResult> ValidateAsync(PackageValidationMessage message, List<PackageValidationAuditEntry> auditEntries)
        {
            var description = $"NuGet - {message.ValidationId} - {message.PackageId} {message.PackageVersion}";

            WriteAuditEntry(auditEntries, $"Submitting virus scan job with description \"{description}\"...");

            string errorMessage;
            try
            {
                var result = await _scanningService.CreateVirusScanJobAsync(
                    BuildStorageUrl(message.Package.Id, message.PackageVersion), _callbackUrl, description, message.ValidationId);

                if (string.IsNullOrEmpty(result.ErrorMessage))
                {
                    WriteAuditEntry(auditEntries, $"Submission completed. Request id: {result.RequestId} - job id: {result.JobId}");
                    return ValidationResult.Asynchronous;
                }
                else
                {
                    errorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }

            WriteAuditEntry(auditEntries, $"Submission failed. Error message: {errorMessage}");
            return ValidationResult.Failed;
        }

        private string BuildStorageUrl(string packageId, string packageVersion)
        {
            // The VCS service needs a blob storage URL, which the NuGet API does not expose.
            // Build one from a template here.
            return _packageUrlTemplate
                .Replace("{id}", packageId)
                .Replace("{version}", packageVersion)
                .ToLowerInvariant();
        }
    }
}