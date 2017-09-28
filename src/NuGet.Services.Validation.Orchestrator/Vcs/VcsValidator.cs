// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs.Validation.Common;
using NuGet.Services.Validation.Orchestrator;
using NuGet.Versioning;

namespace NuGet.Services.Validation.Vcs
{
    public class VcsValidator : IValidator
    {
        private const string ValidatorName = Jobs.Validation.Common.Validators.Vcs.VcsValidator.ValidatorName;

        private readonly IPackageValidationService _validationService;
        private readonly IPackageValidationAuditor _validationAuditor;
        private readonly ILogger<VcsValidator> _logger;

        public VcsValidator(
            IPackageValidationService validationService,
            IPackageValidationAuditor validationAuditor,
            ILogger<VcsValidator> logger)
        {
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _validationAuditor = validationAuditor ?? throw new ArgumentNullException(nameof(validationAuditor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ValidationStatus> GetStatusAsync(IValidationRequest request)
        {
            var audit = await _validationAuditor.ReadAuditAsync(
                request.ValidationId,
                NormalizePackageId(request.PackageId),
                NormalizePackageVersion(request.PackageVersion));

            if (audit == null)
            {
                return ValidationStatus.NotStarted;
            }

            var validationStatusList = audit
                .Entries
                .Where(x => x.ValidatorName == ValidatorName)
                .Select(x => GetValidationStatus(request, x.EventId))
                .ToList();

            return validationStatusList.FirstOrDefault(x => x == ValidationStatus.Failed) ??
                validationStatusList.FirstOrDefault(x => x == ValidationStatus.Succeeded) ??
                ValidationStatus.Incomplete;
        }

        private ValidationStatus? GetValidationStatus(IValidationRequest request, ValidationEvent validationEvent)
        {
            switch (validationEvent)
            {
                case ValidationEvent.ValidatorException:
                case ValidationEvent.BeforeVirusScanRequest:
                case ValidationEvent.VirusScanRequestSent:
                    return ValidationStatus.Incomplete;
                case ValidationEvent.PackageClean:
                    return ValidationStatus.Succeeded;
                case ValidationEvent.PackageNotClean:
                case ValidationEvent.NotCleanReason:
                case ValidationEvent.ScanFailed:
                case ValidationEvent.ScanFailureReason:
                    _logger.LogError(
                        Error.VcsValidationFailureAuditFound,
                        "A failed audit result was found for {validationId} ({packageId} {packageVersion}): {validationEvent}.",
                        request.ValidationId,
                        request.PackageId,
                        request.PackageVersion,
                        validationEvent);
                    return ValidationStatus.Failed;
                default:
                    _logger.LogError(
                        Error.VcsValidationUnexpectedAuditFound,
                        "An unexpected audit result was found for {validationId} ({packageId} {packageVersion}): {validationEvent}.",
                        request.ValidationId,
                        request.PackageId,
                        request.PackageVersion,
                        validationEvent);
                    return ValidationStatus.Failed;
            }
        }

        public async Task<ValidationStatus> StartValidationAsync(IValidationRequest request)
        {
            var normalizedPackageId = NormalizePackageId(request.PackageId);
            var normalizedPackageVerison = NormalizePackageVersion(request.PackageVersion);

            try
            {
                await _validationService.StartValidationProcessAsync(
                    new NuGetPackage
                    {
                        Id = normalizedPackageId,
                        NormalizedVersion = normalizedPackageVerison,
                        Version = normalizedPackageVerison,
                        DownloadUrl = new Uri(request.NupkgUrl),
                    },
                    new[] { ValidatorName },
                    request.ValidationId);
            }
            catch (StorageException e) when (e.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.Conflict
                                             || e.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.PreconditionFailed)
            {
                // This means the validation has already started. This is acceptable so we should move on.
                _logger.LogWarning(
                    Error.VcsValidationAlreadyStarted,
                    e,
                    "The VCS validation for {validationId} ({packageId} {packageVersion}) has already been started.",
                    request.ValidationId,
                    request.PackageId,
                    request.PackageVersion);
            }

            return await GetStatusAsync(request);
        }

        private static string NormalizePackageVersion(string packageVersion)
        {
            return NuGetVersion
                .Parse(packageVersion)
                .ToNormalizedString()
                .ToLowerInvariant();
        }

        private static string NormalizePackageId(string packageId)
        {
            return packageId.ToLowerInvariant();
        }
    }
}
