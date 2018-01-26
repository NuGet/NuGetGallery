// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Services.Validation.Orchestrator;

namespace NuGet.Services.Validation.PackageCertificates
{
    public class PackageCertificatesValidator : IValidator
    {
        private static readonly TimeSpan DefaultCertificateRevalidationThresholdTime = TimeSpan.FromDays(1);

        private readonly IValidationEntitiesContext _validationContext;
        private readonly IValidatorStateService _validatorStateService;
        private readonly ICertificateVerificationEnqueuer _certificateVerificationEnqueuer;
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
            ILogger<PackageCertificatesValidator> logger,
            TimeSpan? certificateRevalidationThreshold = null)
        {
            _validationContext = validationContext ?? throw new ArgumentNullException(nameof(validationContext));
            _validatorStateService = validatorStateService ?? throw new ArgumentNullException(nameof(validatorStateService));
            _certificateVerificationEnqueuer = certificateVerificationEnqueuer ?? throw new ArgumentNullException(nameof(certificateVerificationEnqueuer));
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

            // All of the requested certificate validations have finished. Fail the validation if any
            // signatures have been invalidated.
            var signatures = await FindSignaturesAsync(request);

            foreach (var signature in signatures)
            {
                // Signatures at this point MUST have a state of either "Unknown" or "Invalid" at this point as the
                // PackageSigningValidator will set all signatures to an "Unknown" status, and the ValidateCertificate
                // job may set signatures to the "Invalid" state.
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

                if (signature.Status != PackageSignatureStatus.Unknown)
                {
                    _logger.LogError(
                        Error.PackageCertificateValidationInvalidSignatureState,
                        "Failing validation {ValidationId} ({PackageId} {PackageVersion}) due to invalid signature status: {SignatureStatus}",
                        request.ValidationId,
                        request.PackageId,
                        request.PackageVersion,
                        signature.Status);

                    return await _validatorStateService.TryUpdateValidationStatusAsync(request, status, ValidationStatus.Failed);
                }
            }

            // All signatures are valid. Promote signatures out of the "Unknown" state to either "Valid" or "InGracePeriod".
            PromoteSignatures(signatures);

            return await _validatorStateService.TryUpdateValidationStatusAsync(request, status, ValidationStatus.Succeeded);
        }

        public async Task<IValidationResult> StartValidationAsync(IValidationRequest request)
        {
            var validatorStatus = await StartValidationInternalAsync(request);

            return validatorStatus.ToValidationResult();
        }

        private async Task<ValidatorStatus> StartValidationInternalAsync(IValidationRequest request)
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
            var signatures = await FindSignaturesAsync(request);
            var invalidSignatures = FindSignaturesToInvalidate(signatures, isRevalidationRequest);

            if (invalidSignatures.Any())
            {
                InvalidatePackageSignatures(request, package, invalidSignatures);

                return await _validatorStateService.TryAddValidatorStatusAsync(request, status, ValidationStatus.Failed);
            }

            // Find the certificates that must be validated. A certificate must be validated if it has never been validated,
            // or, if it hasn't been validated in a while (and it hasn't been revoked).
            var certificates = FindCertificatesToValidateAsync(signatures, isRevalidationRequest);

            if (certificates.Any())
            {
                await StartCertificateValidationsAsync(request, certificates);

                return await _validatorStateService.TryAddValidatorStatusAsync(request, status, ValidationStatus.Incomplete);
            }
            else
            {
                _logger.LogInformation(
                    "All certificates for package {PackageId} {PackageVersion} have already been validated, no additional validations necessary",
                    request.PackageId,
                    request.PackageVersion);

                // Promote signatures out of the "Unknown" state to either "Valid" or "InGracePeriod".
                PromoteSignatures(signatures);

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
        private Task<List<PackageSignature>> FindSignaturesAsync(IValidationRequest request)
        {
            return _validationContext
                        .PackageSignatures
                        .Where(s => s.PackageKey == request.PackageKey)
                        .Include(s => s.TrustedTimestamps)
                        .Include(s => s.EndCertificate)
                        .ToListAsync();
        }

        /// <summary>
        /// Promote valid signatures from "Unknown" status to either "Valid" or "InGracePeriod".
        /// </summary>
        /// <param name="signatures">The valid signatures that should be promoted.</param>
        private void PromoteSignatures(IEnumerable<PackageSignature> signatures)
        {
            foreach (var signature in signatures)
            {
                signature.Status = IsValidSignatureOutOfGracePeriod(signature)
                    ? PackageSignatureStatus.Valid
                    : PackageSignatureStatus.InGracePeriod;
            }
        }

        /// <summary>
        /// Decide whether the valid signature should be considered "Valid" or "InGracePeriod".
        /// </summary>
        /// <param name="signature">The valid signature whose status should be decided.</param>
        /// <returns>True if the signature should be "Valid", false if it should be "InGracePeriod".</returns>
        private bool IsValidSignatureOutOfGracePeriod(PackageSignature signature)
        {
            var certificate = signature.EndCertificate;

            // A signature can be valid even if its certificate is revoked as long as the certificate
            // revocation date begins after the signature was created. The validation pipeline does
            // not revalidate revoked certificates, thus, a valid package signature with a revoked
            // certificate should be "Valid" regardless of the certificate's status update time.
            if (certificate.Status == EndCertificateStatus.Revoked)
            {
                return true;
            }
            else if (certificate.StatusUpdateTime.HasValue)
            {
                var signatureTime = signature.TrustedTimestamps.Max(t => t.Value);

                return certificate.StatusUpdateTime > signatureTime;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Find the signatures that should be invalidated.
        /// </summary>
        /// <param name="signatures">The package's signatures.</param>
        /// <param name="isRevalidationRequest">Whether this package has already been validated. If true, invalid certificates will not invalidate signatures.</param>
        /// <returns>The signatures whose certificates' state invalidates the signatrure.</returns>
        private IEnumerable<PackageSignature> FindSignaturesToInvalidate(IEnumerable<PackageSignature> signatures, bool isRevalidationRequest)
        {
            return signatures
                    .Where(s =>
                    {
                        // Revalidation requests do NOT revalidate certificates that are known to be revoked. Thus,
                        // certificates that were revoked before the package was signed ALWAYS invalidate the signature.
                        if (s.EndCertificate.Status == EndCertificateStatus.Revoked)
                        {
                            return s.TrustedTimestamps.Any(t => s.EndCertificate.RevocationTime.Value <= t.Value);
                        }

                        // Revalidation requests will revalidate invalid certificates. Therefore, invalid certificates
                        // should invalidate the signature only if this is not a revalidation request.
                        if (s.EndCertificate.Status == EndCertificateStatus.Invalid)
                        {
                            return !isRevalidationRequest;
                        }

                        return false;
                    })
                    .ToList();
        }

        /// <summary>
        /// Mark a package's signing state and its revoked signatures as invalid. This method does NOT
        /// persist entity changes to the database.
        /// </summary>
        /// <param name="request">The request to validate a package.</param>
        /// <param name="package">The package's overall signing state that should be invalidated.</param>
        /// <param name="revokedSignatures">The package's signatures that should be invalidated.</param>
        /// <returns>A task that completes when the entities have been updated.</returns>
        private void InvalidatePackageSignatures(IValidationRequest request, PackageSigningState package, IEnumerable<PackageSignature> revokedSignatures)
        {
            _logger.LogWarning(
                "Invalidating package {PackageId} {PackageVersion} due to revoked signatures.",
                request.PackageId,
                request.PackageVersion);

            package.SigningStatus = PackageSigningStatus.Invalid;

            foreach (var signature in revokedSignatures)
            {
                signature.Status = PackageSignatureStatus.Invalid;
            }
        }

        /// <summary>
        /// Find all the certificates that should be validated from the given signatures.
        /// </summary>
        /// <param name="signatures">The signatures used to sign the package requested by the validation request.</param>
        /// <param name="isRevalidationRequest">Whether this package has already been validated.</param>
        /// <returns>The certificates used to sign the package that should be validated.</returns>
        private IEnumerable<EndCertificate> FindCertificatesToValidateAsync(IEnumerable<PackageSignature> signatures, bool isRevalidationRequest)
        {
            // Get all the certificates used to sign the signatures. Note that revoked certificates
            // should NEVER be revalidated as Certificate Authorities may, under certain conditions,
            // drop a revoked certificate's revocation information. Revalidating such a revoked
            // certificate would cause the certificate to be marked as "Good" when in reality it
            // should remain revoked.
            var certificates = signatures.Select(s => s.EndCertificate).Where(c => c.Status != EndCertificateStatus.Revoked);

            // Skip certificates that have been validated recently unless this is a revalidation request.
            if (!isRevalidationRequest)
            {
                certificates = certificates.Where(ShouldValidateCertificate);
            }

            return certificates.ToList();
        }

        /// <summary>
        /// Decide whether or not to revalidate the given certificate.
        /// </summary>
        /// <param name="certificate">The certificate that may be revalidated.</param>
        /// <returns>Whether the certificate should be revalidated.</returns>
        private bool ShouldValidateCertificate(EndCertificate certificate)
        {
            // Validate the certificate only if it has never been validated before, or, if
            // its last validation time is past the maximum revalidation threshold.
            if (certificate.LastVerificationTime.HasValue)
            {
                var timeAgo = DateTime.UtcNow - certificate.LastVerificationTime.Value;

                return timeAgo >= _certificateRevalidationThresholdTime;
            }

            return true;
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