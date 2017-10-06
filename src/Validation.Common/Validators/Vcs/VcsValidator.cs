// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.VirusScanning.Vcs;

namespace NuGet.Jobs.Validation.Common.Validators.Vcs
{
    public class VcsValidator
        : ValidatorBase, IValidator
    {
        public const string ValidatorName = "validator-vcs";

        private readonly Uri _callbackUrl;
        private readonly VcsVirusScanningService _scanningService;

        private readonly ILogger<VcsValidator> _logger;

        public VcsValidator(string serviceUrl, string callbackUrl, string contactAlias, string submitterAlias, string packageUrlTemplate, ILoggerFactory loggerFactory)
            : base(packageUrlTemplate)
        {
            _logger = loggerFactory.CreateLogger<VcsValidator>();
            _scanningService = new VcsVirusScanningService(
                new Uri(serviceUrl),
                "DIRECT",
                contactAlias,
                submitterAlias,
                loggerFactory);
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
            _logger.LogInformation("Submitting virus scan job with description {description}, " +
                    $" {{{TraceConstant.ValidatorName}}} {{{TraceConstant.ValidationId}}} " +
                    $" for package {{{TraceConstant.PackageId}}} " +
                    $"v. {{{TraceConstant.PackageVersion}}}", 
                description,
                Name,
                message.ValidationId,
                message.PackageId,
                message.PackageVersion);
            WriteAuditEntry(auditEntries, $"Submitting virus scan job with description \"{description}\"...",
                ValidationEvent.BeforeVirusScanRequest);

            string errorMessage;
            try
            {
                var packageUrl = GetPackageUrl(message);

                // VCS requires a package URL that is either a direct URL to Azure Blob Storage or a UNC share file
                // path. Azure Blob Storage URLs with SAS tokens in them are accepted.
                var result = await _scanningService.CreateVirusScanJobAsync(
                    packageUrl,
                    _callbackUrl,
                    description,
                    message.ValidationId);

                if (string.IsNullOrEmpty(result.ErrorMessage))
                {
                    _logger.LogInformation("Submission completed for " +
                        $"{{{TraceConstant.ValidatorName}}} {{{TraceConstant.ValidationId}}}. " +
                        $"package {{{TraceConstant.PackageId}}} " +
                        $"{{{TraceConstant.PackageVersion}}} " +
                        "Request id: {RequestId} - job id: {JobId} - region code: {RegionCode}", 
                        Name,
                        message.ValidationId,
                        message.PackageId,
                        message.PackageVersion,
                        result.RequestId,
                        result.JobId,
                        result.RegionCode);
                    WriteAuditEntry(auditEntries, $"Submission completed. Request id: {result.RequestId} " +
                        $"- job id: {result.JobId} " +
                        $"- region code: {result.RegionCode}",
                        ValidationEvent.VirusScanRequestSent);
                    return ValidationResult.Asynchronous;
                }
                else
                {
                    errorMessage = result.ErrorMessage;

                    _logger.LogError($"Submission failed for {{{TraceConstant.ValidatorName}}} {{{TraceConstant.ValidationId}}} " +
                            $"package {{{TraceConstant.PackageId}}} " +
                            $"v. {{{TraceConstant.PackageVersion}}} " +
                            "with: {ErrorMessage}",
                        Name,
                        message.ValidationId,
                        message.PackageId,
                        message.PackageVersion,
                        errorMessage);
                    WriteAuditEntry(auditEntries, $"Submission failed. Error message: {errorMessage}",
                        ValidationEvent.VirusScanRequestFailed);

                    throw new ValidationException(errorMessage);
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                _logger.TrackValidatorException(ValidatorName, message.ValidationId, ex, message.PackageId, message.PackageVersion);
                WriteAuditEntry(auditEntries, $"Submission failed. Error message: {errorMessage}",
                    ValidationEvent.VirusScanRequestFailed);
                throw;
            }
        }
    }
}