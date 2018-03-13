// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Services.Validation.Orchestrator;
using NuGet.Services.Validation.Orchestrator.Telemetry;

namespace NuGet.Services.Validation.PackageCertificates
{
    public class PackageCertificatesValidator : BaseValidator, IValidator
    {
        private static readonly TimeSpan DefaultCertificateRevalidationThresholdTime = TimeSpan.FromDays(1);

        private readonly IValidationEntitiesContext _validationContext;
        private readonly IValidatorStateService _validatorStateService;
        private readonly ICertificateVerificationEnqueuer _certificateVerificationEnqueuer;
        private readonly ITelemetryService _telemetryService;
        private readonly TimeSpan _certificateRevalidationThresholdTime;
        private readonly ILogger<PackageCertificatesValidator> _logger;

        /// <summary>
        /// Instantiate a new package certificates validator.
        /// </summary>
        /// <param name="validationContext">The persisted validation context.</param>
        /// <param name="validatorStateService">The service used to persist this validator's state.</param>
        /// <param name="certificateVerificationEnqueuer">The verifier used to verify individual certificates asynchronously.</param>
        /// <param name="logger">The logginator.</param>
        /// <param name="certificateRevalidationThreshold">How stale certificates' statuses can be before revalidating. Defaults to 1 day.</param>
        public PackageCertificatesValidator(
            IValidationEntitiesContext validationContext,
            IValidatorStateService validatorStateService,
            ICertificateVerificationEnqueuer certificateVerificationEnqueuer,
            ITelemetryService telemetryService,
            ILogger<PackageCertificatesValidator> logger,
            TimeSpan? certificateRevalidationThreshold = null)
        {
            _validationContext = validationContext ?? throw new ArgumentNullException(nameof(validationContext));
            _validatorStateService = validatorStateService ?? throw new ArgumentNullException(nameof(validatorStateService));
            _certificateVerificationEnqueuer = certificateVerificationEnqueuer ?? throw new ArgumentNullException(nameof(certificateVerificationEnqueuer));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _certificateRevalidationThresholdTime = certificateRevalidationThreshold ?? DefaultCertificateRevalidationThresholdTime;

            if (_certificateRevalidationThresholdTime < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(certificateRevalidationThreshold), "The certificate revalidation threshold time must be a positive value");
            }
        }

        public async Task<IValidationResult> GetResultAsync(IValidationRequest request)
        {
            var status = await GetStatusAsync(request);

            return status.ToValidationResult();
        }

        private async Task<ValidatorStatus> GetStatusAsync(IValidationRequest request)
        {
            // Look up this validator's state in the database.
            var status = await _validatorStateService.GetStatusAsync(request);

            if (status.State != ValidationStatus.Incomplete)
            {
                return status;
            }

            // Wait until ALL certificate validations kicked off by this validation request have finished.
            if (!await AllCertificateValidationsAreFinishedAsync(request))
            {
                // We know this status is incomplete.
                return status;
            }

            // All of the requested certificate validations have finished. At this point, the signature
            // may have a status of "Unknown" if the package is at ingestion and its signature has passed
            // all validations, "Invalid" if one or more of the signature's certificates has failed validations,
            // or "InGracePeriod" or "Valid" if this is a revalidation request.
            var signature = await FindSignatureAsync(request);

            if (signature.Status == PackageSignatureStatus.Invalid)
            {
                _logger.LogWarning(
                    "Failing validation {ValidationId} ({PackageId} {PackageVersion}) due to invalidated signature: {SignatureKey}",
                    request.ValidationId,
                    request.PackageId,
                    request.PackageVersion,
                    signature.Key);

                return await _validatorStateService.TryUpdateValidationStatusAsync(request, status, ValidationStatus.Failed);
            }
            else
            {
                _logger.LogInformation(
                    "Successful validation {ValidationId} ({PackageId} {PackageVersion})",
                    request.ValidationId,
                    request.PackageId,
                    request.PackageVersion);

                PromoteSignature(request, signature);

                return await _validatorStateService.TryUpdateValidationStatusAsync(request, status, ValidationStatus.Succeeded);
            }
        }

        public async Task<IValidationResult> StartAsync(IValidationRequest request)
        {
            var validatorStatus = await StartInternalAsync(request);

            return validatorStatus.ToValidationResult();
        }

        private async Task<ValidatorStatus> StartInternalAsync(IValidationRequest request)
        {
            var status = await _validatorStateService.GetStatusAsync(request);

            if (status.State != ValidationStatus.NotStarted)
            {
                _logger.LogWarning(
                    "Package Certificates validation with validationId {ValidationId} ({PackageId} {PackageVersion}) has already started.",
                    request.ValidationId,
                    request.PackageId,
                    request.PackageVersion);

                return status;
            }

            var package = await FindPackageSigningStateAsync(request);

            if (package.SigningStatus == PackageSigningStatus.Unsigned)
            {
                _logger.LogInformation(
                    "Package {PackageId} {PackageVersion} is unsigned, no additional validations necessary",
                    request.PackageId,
                    request.PackageVersion);

                return await _validatorStateService.TryAddValidatorStatusAsync(request, status, ValidationStatus.Succeeded);
            }
            else if (package.SigningStatus == PackageSigningStatus.Invalid)
            {
                // Do NOT validate the package if its status is already marked as invalid. To revalidate the package,
                // it MUST first pass the PackageSigningValidator. The PackageSigningValidator will mark the package's
                // signing status as Valid, thereby allowing this validator to revalidate the package if necessary.
                _logger.LogError(
                    Error.PackageCertificateValidationAlreadyFailed,
                    "Package {PackageId} {PackageVersion} has already failed validation",
                    request.PackageId,
                    request.PackageVersion);

                return await _validatorStateService.TryAddValidatorStatusAsync(request, status, ValidationStatus.Failed);
            }

            var isRevalidationRequest = await _validatorStateService.IsRevalidationRequestAsync(request);

            // Find the signatures used to sign the package and see if any certificates known to be revoked
            // invalidate any of these signatures. Note that a revoked certificate is assumed to remain
            // revoked forever.
            var signature = await FindSignatureAsync(request);

            if (ShouldInvalidateSignature(signature, isRevalidationRequest))
            {
                InvalidatePackageSignature(request, package, signature);

                return await _validatorStateService.TryAddValidatorStatusAsync(request, status, ValidationStatus.Failed);
            }

            // Find the certificates that must be validated. A certificate must be validated if it has never been validated,
            // or, if it hasn't been validated in a while (and it hasn't been revoked).
            var certificates = FindCertificatesToValidateAsync(signature, isRevalidationRequest);

            if (certificates.Any())
            {
                var stopwatch = Stopwatch.StartNew();

                await StartCertificateValidationsAsync(request, certificates);

                var result = await _validatorStateService.TryAddValidatorStatusAsync(request, status, ValidationStatus.Incomplete);

                _telemetryService.TrackDurationToStartPackageCertificatesValidator(stopwatch.Elapsed);

                return result;
            }
            else
            {
                PromoteSignature(request, signature);

                return await _validatorStateService.TryAddValidatorStatusAsync(request, status, ValidationStatus.Succeeded);
            }
        }

        /// <summary>
        /// Check whether all certificate validations for the given validation request are finished.
        /// </summary>
        /// <param name="request">The validation request that started the certificate validations.</param>
        /// <returns>Whether the certificate validations are ALL finished.</returns>
        private Task<bool> AllCertificateValidationsAreFinishedAsync(IValidationRequest request)
        {
            // Incomplete CertificateValidation have a Status of NULL.
            return _validationContext
                        .CertificateValidations
                        .Where(v => v.ValidationId == request.ValidationId)
                        .AllAsync(v => v.Status.HasValue);
        }

        /// <summary>
        /// Find the state of a package's signing.
        /// </summary>
        /// <param name="request">The validation request containing the package whose signing state should be fetched.</param>
        /// <returns>The package's signing state.</returns>
        private Task<PackageSigningState> FindPackageSigningStateAsync(IValidationRequest request)
        {
            return _validationContext
                        .PackageSigningStates
                        .Where(p => p.PackageKey == request.PackageKey)
                        .FirstAsync();
        }

        /// <summary>
        /// Find all of the signatures and their certificates for the given validation request's package.
        /// </summary>
        /// <param name="request">The validation request containing the package whose signatures should be fetched.</param>
        /// <returns>The package's signatures with their certificates.</returns>
        private Task<PackageSignature> FindSignatureAsync(IValidationRequest request)
        {
            return _validationContext
                        .PackageSignatures
                        .Include(s => s.EndCertificate)
                        .Include(s => s.TrustedTimestamps.Select(t => t.EndCertificate))
                        .SingleAsync(s => s.PackageKey == request.PackageKey);
        }

        /// <summary>
        /// Promote valid signatures from "Unknown" status to either "Valid" or "InGracePeriod".
        /// </summary>
        /// <param name="request">The validation request for the package whose signature should be promoted.</param>
        /// <param name="signatures">The valid signatures that should be promoted.</param>
        private void PromoteSignature(IValidationRequest request, PackageSignature signature)
        {

            var newSignatureStatus = (IsValidSignatureOutOfGracePeriod(request, signature))
                                        ? PackageSignatureStatus.Valid
                                        : PackageSignatureStatus.InGracePeriod;

            _logger.LogInformation(
                "Promoting package {PackageId} {PackageVersion} signature from status {OldSignatureStatus} to status {NewSignatureStatus}",
                request.PackageId,
                request.PackageVersion,
                signature.Status,
                newSignatureStatus);

            signature.Status = newSignatureStatus;
        }

        /// <summary>
        /// Decide whether the valid signature should be considered "Valid" or "InGracePeriod".
        /// </summary>
        /// <param name="request">The validation request for the package whose signature should be inspected.</param>
        /// <param name="signature">The valid signature whose status should be decided.</param>
        /// <returns>True if the signature should be "Valid", false if it should be "InGracePeriod".</returns>
        private bool IsValidSignatureOutOfGracePeriod(IValidationRequest request, PackageSignature signature)
        {
            bool IsCertificateStatusPastTime(EndCertificate certificate, DateTime time)
            {
                return (certificate.StatusUpdateTime.HasValue && certificate.StatusUpdateTime > time);
            }

            var signingTime = signature.TrustedTimestamps.Max(t => t.Value);

            // Ensure the timestamps' certificate statuses are fresher than the signature.
            foreach (var timestamp in signature.TrustedTimestamps)
            {
                // A valid signature should NEVER have a timestamp whose end certificate is revoked.
                // Note that it is possible for a valid signature to have an invalid certificate as
                // certain certificate statuses, like "NotTimeNested", do not affect signatures.
                if (timestamp.EndCertificate.Status == EndCertificateStatus.Revoked)
                {
                    _logger.LogError(
                        Error.PackageCertificateValidationInvalidSignatureState,
                        "Valid signature cannot have a timestamp whose end certificate is revoked ({ValidationId}, {PackageId} {PackageVersion})",
                        request.ValidationId,
                        request.PackageId,
                        request.PackageVersion,
                        signature.Status);

                    throw new InvalidOperationException(
                        $"ValidationId {request.ValidationId} has valid signature with a timestamp whose end certificate is revoked");
                }

                if (!IsCertificateStatusPastTime(timestamp.EndCertificate, signingTime))
                {
                    return false;
                }
            }

            // A signature can be valid even if its certificate is revoked as long as the certificate
            // revocation date begins after the signature was created. The validation pipeline does
            // not revalidate revoked certificates, thus, a valid package signature with a revoked
            // certificate is considered out of the grace period regardless of the certificate's
            // status update time.
            if (signature.EndCertificate.Status != EndCertificateStatus.Revoked)
            {
                // Ensure the signature's certificate status is fresher than the signature.
                if (!IsCertificateStatusPastTime(signature.EndCertificate, signingTime))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Decide whether the signature should be invalidated.
        /// </summary>
        /// <param name="signature">The package's signature.</param>
        /// <param name="isRevalidationRequest">Whether this package has already been validated. If true, invalid certificates will not invalidate signatures.</param>
        /// <returns>Whether the signature should be invalidated.</returns>
        private bool ShouldInvalidateSignature(PackageSignature signature, bool isRevalidationRequest)
        {
            // Revalidation requests do NOT revalidate certificates that are known to be revoked. Thus,
            // certificates that were revoked before the package was signed ALWAYS invalidate the signature.
            if (signature.EndCertificate.Status == EndCertificateStatus.Revoked)
            {
                return signature.TrustedTimestamps.Any(t => signature.EndCertificate.RevocationTime.Value <= t.Value);
            }

            // Revalidation requests will revalidate invalid certificates. Therefore, invalid certificates
            // should invalidate the signature only if this is not a revalidation request.
            if (signature.EndCertificate.Status == EndCertificateStatus.Invalid)
            {
                return !isRevalidationRequest;
            }

            return false;
        }

        /// <summary>
        /// Mark a package's signing state and its revoked signatures as invalid. This method does NOT
        /// persist entity changes to the database.
        /// </summary>
        /// <param name="request">The request to validate a package.</param>
        /// <param name="package">The package's overall signing state that should be invalidated.</param>
        /// <param name="signature">The package's signatures that should be invalidated.</param>
        /// <returns>A task that completes when the entities have been updated.</returns>
        private void InvalidatePackageSignature(IValidationRequest request, PackageSigningState package, PackageSignature signature)
        {
            _logger.LogWarning(
                "Invalidating package {PackageId} {PackageVersion} due to revoked signatures.",
                request.PackageId,
                request.PackageVersion);

            package.SigningStatus = PackageSigningStatus.Invalid;
            signature.Status = PackageSignatureStatus.Invalid;
        }

        /// <summary>
        /// Find all the certificates that should be validated from the given signature.
        /// </summary>
        /// <param name="signature">The signature whose certificates should be found.</param>
        /// <param name="isRevalidationRequest">Whether this package has already been validated.</param>
        /// <returns>The certificates used to sign the package that should be validated.</returns>
        private IEnumerable<EndCertificate> FindCertificatesToValidateAsync(PackageSignature signature, bool isRevalidationRequest)
        {
            var certificates = new List<EndCertificate>();

            certificates.Add(signature.EndCertificate);
            certificates.AddRange(signature.TrustedTimestamps.Select(t => t.EndCertificate));

            // Revoked certificates should NEVER be revalidated as Certificate Authorities may,
            // under certain conditions, drop a revoked certificate's revocation information.
            // Revalidating such a revoked certificate would cause the certificate to be marked as
            // "Good" when in reality it should remain revoked.
            var result = certificates.Where(c => c.Status != EndCertificateStatus.Revoked);

            // Allow normal validations to skip verifying certificates that have been recently validated.
            if (!isRevalidationRequest)
            {
                result = result.Where(c =>
                {
                    if (c.LastVerificationTime.HasValue)
                    {
                        var timeAgo = DateTime.UtcNow - c.LastVerificationTime.Value;

                        return timeAgo >= _certificateRevalidationThresholdTime;
                    }

                    return true;
                });
            }

            return result.ToList();
        }

        /// <summary>
        /// Enqueue certificate verifications and add <see cref="EndCertificateValidation"/> entities
        /// for each validation. Note that this does NOT save the entity context!
        /// </summary>
        /// <param name="request">The package validation request.</param>
        /// <param name="certificates">The certificates that should be verified.</param>
        /// <returns>A task that completes when all certificate verifications have been enqueued.</returns>
        private Task StartCertificateValidationsAsync(IValidationRequest request, IEnumerable<EndCertificate> certificates)
        {
            var startCertificateVerificationTasks = new List<Task>();

            foreach (var certificate in certificates)
            {
                startCertificateVerificationTasks.Add(_certificateVerificationEnqueuer.EnqueueVerificationAsync(request, certificate));

                _validationContext.CertificateValidations.Add(new EndCertificateValidation
                {
                    ValidationId = request.ValidationId,
                    EndCertificate = certificate,
                    Status = null,
                });
            }

            return Task.WhenAll(startCertificateVerificationTasks);
        }
    }
}