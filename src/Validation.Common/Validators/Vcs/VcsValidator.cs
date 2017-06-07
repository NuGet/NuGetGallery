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

        private readonly string _packageUrlTemplate;
        private readonly Uri _callbackUrl;
        private readonly VcsVirusScanningService _scanningService;

        private readonly ILogger<VcsValidator> _logger;

        public VcsValidator(string serviceUrl, string callbackUrl, string contactAlias, string submitterAlias, string packageUrlTemplate, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<VcsValidator>();
            _packageUrlTemplate = packageUrlTemplate;
            _scanningService = new VcsVirusScanningService(new Uri(serviceUrl), "DIRECT", contactAlias, submitterAlias);
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
                var result = await _scanningService.CreateVirusScanJobAsync(
                    BuildStorageUrl(message.Package.Id, message.PackageVersion), _callbackUrl, description, message.ValidationId);

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
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                _logger.TrackValidatorException(ValidatorName, message.ValidationId, ex, message.PackageId, message.PackageVersion);
            }

            WriteAuditEntry(auditEntries, $"Submission failed. Error message: {errorMessage}", 
                ValidationEvent.VirusScanRequestFailed);
            return ValidationResult.Failed;
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