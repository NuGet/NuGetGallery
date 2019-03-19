// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Services.Validation;

namespace Validation.PackageSigning.ValidateCertificate
{
    public class CertificateValidationService : ICertificateValidationService
    {
        internal const int DefaultMaximumValidationFailures = 10;
        private const int MaxSignatureUpdatesPerTransaction = 500;

        private readonly IValidationEntitiesContext _context;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<CertificateValidationService> _logger;
        private readonly int _maximumValidationFailures;

        private readonly SignatureDeciderFactory _signatureDeciderFactory;

        public CertificateValidationService(
            IValidationEntitiesContext context,
            ITelemetryService telemetryService,
            ILogger<CertificateValidationService> logger,
            int maximumValidationFailures = DefaultMaximumValidationFailures)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _maximumValidationFailures = maximumValidationFailures;

            _signatureDeciderFactory = new SignatureDeciderFactory();
        }

        public Task<EndCertificateValidation> FindCertificateValidationAsync(CertificateValidationMessage message)
        {
            // Fetch the validation, the end certificate that this validation is for, and all of the parent
            // certificates that the end certificate depends on.
            return _context
                        .CertificateValidations
                        .Where(v => v.ValidationId == message.ValidationId && v.EndCertificateKey == message.CertificateKey)
                        .Include(v => v.EndCertificate.CertificateChainLinks.Select(l => l.ParentCertificate))
                        .FirstOrDefaultAsync();
        }

        public async Task<bool> TrySaveResultAsync(EndCertificateValidation validation, CertificateVerificationResult result)
        {
            if (validation.EndCertificate.Status == EndCertificateStatus.Revoked && result.Status != EndCertificateStatus.Revoked)
            {
                _logger.LogWarning(
                    "Updating previously revoked certificate {CertificateThumbprint} to status {NewStatus}",
                    validation.EndCertificate.Thumbprint,
                    result.Status);
            }
            else
            {
                _logger.LogInformation(
                    "Updating certificate {CertificateThumbprint} to status {NewStatus}",
                    validation.EndCertificate.Thumbprint,
                    result.Status);
            }

            try
            {
                switch (result.Status)
                {
                    case EndCertificateStatus.Good:
                        await SaveGoodCertificateStatusAsync(validation, result);
                        break;

                    case EndCertificateStatus.Unknown:
                        await SaveUnknownCertificateStatusAsync(validation);
                        break;

                    case EndCertificateStatus.Invalid:
                        await SaveInvalidCertificateStatusAsync(validation, result);
                        break;

                    case EndCertificateStatus.Revoked:
                        await SaveRevokedCertificateStatusAsync(validation, result);
                        break;

                    default:
                        _logger.LogError(
                            $"Unknown {nameof(EndCertificateStatus)} value: {{CertificateStatus}}, throwing to retry",
                            result.Status);

                        throw new NotSupportedException($"Unknown {nameof(EndCertificateStatus)} value: {result.Status}");
                }

                return true;
            }
            catch (DbUpdateConcurrencyException e)
            {
                // The update concurrency exception be triggered by either the Certificate record or one of the dependent
                // PackageSignature records. Regardless, retry the validation so that the Certificate is validated with
                // the new state.
                _logger.LogWarning(
                    0,
                    e,
                    "Failed to update certificate {CertificateThumbprint} to status {NewStatus} due to concurrency exception",
                    validation.EndCertificate.Thumbprint,
                    result.Status);

                return false;
            }
        }

        private Task SaveGoodCertificateStatusAsync(EndCertificateValidation validation, CertificateVerificationResult result)
        {
            validation.EndCertificate.Status = EndCertificateStatus.Good;
            validation.EndCertificate.StatusUpdateTime = result.StatusUpdateTime;
            validation.EndCertificate.NextStatusUpdateTime = null;
            validation.EndCertificate.LastVerificationTime = DateTime.UtcNow;
            validation.EndCertificate.RevocationTime = null;
            validation.EndCertificate.ValidationFailures = 0;

            validation.Status = EndCertificateStatus.Good;

            return _context.SaveChangesAsync();
        }

        private Task SaveUnknownCertificateStatusAsync(EndCertificateValidation validation)
        {
            validation.EndCertificate.ValidationFailures++;

            if (validation.EndCertificate.ValidationFailures >= _maximumValidationFailures)
            {
                // The maximum number of validation failures has been reached. The certificate's
                // validation should not be retried as a NuGet Admin will need to investigate the issues.
                // If the certificate is found to be invalid, the Admin will need to invalidate packages
                // and timestamps that depend on this certificate!
                validation.EndCertificate.Status = EndCertificateStatus.Invalid;
                validation.EndCertificate.LastVerificationTime = DateTime.UtcNow;

                validation.Status = EndCertificateStatus.Invalid;

                _logger.LogWarning(
                    "Certificate {CertificateThumbprint} has reached maximum of {MaximumValidationFailures} failed validation attempts " +
                    "and requires manual investigation by NuGet Admin. Firing alert...",
                    validation.EndCertificate.Thumbprint,
                    _maximumValidationFailures);

                _telemetryService.TrackUnableToValidateCertificateEvent(validation.EndCertificate);
            }

            return _context.SaveChangesAsync();
        }

        private Task SaveInvalidCertificateStatusAsync(EndCertificateValidation validation, CertificateVerificationResult result)
        {
            var invalidationDecider = _signatureDeciderFactory.MakeDeciderForInvalidatedCertificate(validation.EndCertificate, result);

            void InvalidateCertificate()
            {
                validation.EndCertificate.Status = EndCertificateStatus.Invalid;
                validation.EndCertificate.StatusUpdateTime = result.StatusUpdateTime;
                validation.EndCertificate.NextStatusUpdateTime = null;
                validation.EndCertificate.LastVerificationTime = DateTime.UtcNow;
                validation.EndCertificate.RevocationTime = null;
                validation.EndCertificate.ValidationFailures = 0;

                validation.Status = EndCertificateStatus.Invalid;
            }

            return ProcessDependentSignaturesAsync(
                        validation.EndCertificate,
                        result,
                        invalidationDecider,
                        onAllSignaturesHandled: InvalidateCertificate);
        }

        private Task SaveRevokedCertificateStatusAsync(EndCertificateValidation validation, CertificateVerificationResult result)
        {
            var invalidationDecider = _signatureDeciderFactory.MakeDeciderForRevokedCertificate(validation.EndCertificate, result);

            void RevokeCertificate()
            {
                validation.EndCertificate.Status = EndCertificateStatus.Revoked;
                validation.EndCertificate.StatusUpdateTime = result.StatusUpdateTime;
                validation.EndCertificate.NextStatusUpdateTime = null;
                validation.EndCertificate.LastVerificationTime = DateTime.UtcNow;
                validation.EndCertificate.RevocationTime = result.RevocationTime;
                validation.EndCertificate.ValidationFailures = 0;

                validation.Status = EndCertificateStatus.Revoked;
            }

            return ProcessDependentSignaturesAsync(
                        validation.EndCertificate,
                        result,
                        invalidationDecider,
                        onAllSignaturesHandled: RevokeCertificate);
        }

        /// <summary>
        /// The helper that processes how a certificate's status change affects its dependent signatures.
        /// </summary>
        /// <param name="certificate">The certificate whose dependent signatures should be processed.</param>
        /// <param name="certificateVerificationResult">The result of the certificate's verification.</param>
        /// <param name="signatureDecider">The delegate that decides how a dependent signature should be handled.</param>
        /// <param name="onAllSignaturesHandled">The action that will be called once all dependent signatures have been processed.</param>
        /// <returns></returns>
        private async Task ProcessDependentSignaturesAsync(
            EndCertificate certificate,
            CertificateVerificationResult certificateVerificationResult,
            SignatureDecider signatureDecider,
            Action onAllSignaturesHandled)
        {
            // A single certificate may be dependend on by many signatures. To ensure sanity, only up
            // to "MaxSignatureUpdatesPerTransaction" signatures will be invalidated at a time.
            List<PackageSignature> signatures = null;
            int page = 0;

            do
            {
                // If necessary, save the previous iteration's signature invalidations.
                if (page > 0)
                {
                    _logger.LogInformation(
                        "Persisting {Signatures} dependent signature updates for certificate {CertificateThumbprint} (page {Page})",
                        signatures.Count,
                        certificate.Thumbprint,
                        page);

                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation(
                    "Finding more dependent signatures to update for certificate {CertificateThumbprint}... (page {Page})",
                    certificate.Thumbprint,
                    page);

                signatures = await FindSignaturesAsync(certificate, page);

                _logger.LogInformation(
                    "Updating {Signatures} signatures for certificate {CertificateThumbprint}... (page {Page})",
                    signatures.Count,
                    certificate.Thumbprint,
                    page);

                foreach (var signature in signatures)
                {
                    var decision = signatureDecider(signature);

                    HandleSignatureDecision(signature, decision, certificate, certificateVerificationResult);
                }

                page++;
            }
            while (signatures.Count == MaxSignatureUpdatesPerTransaction);

            // All signatures have been invalidated. Do any necessary finalizations, and persist the results.
            _logger.LogInformation(
                "Finalizing {Signatures} dependent signature updates for certificate {CertificateThumbprint} (total pages: {Pages})",
                signatures.Count,
                certificate.Thumbprint,
                page + 1);

            onAllSignaturesHandled();

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Find all package signatures that depend on the given certificate. This method will return signatures in
        /// batches of size <see cref="MaxSignatureUpdatesPerTransaction"/>.
        /// </summary>
        /// <param name="certificate">The certificate whose signatures should be found.</param>
        /// <param name="page">Which page of signatures should be fetched.</param>
        /// <returns>The signatures that depend on the given certificate.</returns>
        private Task<List<PackageSignature>> FindSignaturesAsync(EndCertificate certificate, int page)
        {
            // A signature may depend on a certificate in one of two ways: the signature itself may have been signed using
            // the certificate, or, one of the signature's trusted timestamps may have been signed using the certificate.
            IQueryable<PackageSignature> packageSignatures;

            switch (certificate.Use)
            {
                case EndCertificateUse.CodeSigning:
                    packageSignatures = _context.PackageSignatures
                                                .Where(s => s.Type == PackageSignatureType.Author)
                                                .Where(s => s.EndCertificate.Thumbprint == certificate.Thumbprint);
                    break;

                case EndCertificateUse.Timestamping:
                    packageSignatures = _context.PackageSignatures
                                                .Where(s => s.Type == PackageSignatureType.Author)
                                                .Where(s => s.TrustedTimestamps.Any(t => t.EndCertificate.Thumbprint == certificate.Thumbprint));

                    break;

                default:
                    throw new InvalidOperationException($"Unknown {nameof(EndCertificateUse)}: {certificate.Use}");
            }

            return packageSignatures
                        .Include(s => s.TrustedTimestamps.Select(t => t.EndCertificate))
                        .Include(s => s.PackageSigningState)
                        .OrderBy(s => s.Key)
                        .Skip(page * MaxSignatureUpdatesPerTransaction)
                        .Take(MaxSignatureUpdatesPerTransaction)
                        .ToListAsync();
        }


        /// <summary>
        /// Handle the decision on how to update the signature.
        /// </summary>
        /// <param name="signature">The signature that should be updated.</param>
        /// <param name="decision">How the signature should be updated.</param>
        /// <param name="certificate">The certificate that signature depends on that changed the signature's state.</param>
        /// <param name="certificateVerificationResult">The certificate verification that changed the signature's state.</param>
        private void HandleSignatureDecision(
            PackageSignature signature,
            SignatureDecision decision,
            EndCertificate certificate,
            CertificateVerificationResult certificateVerificationResult)
        {
            switch (decision)
            {
                case SignatureDecision.Ignore:
                    _logger.LogInformation(
                        "Signature {SignatureKey} is not affected by certificate verification result: {CertificateVerificationResult}",
                        signature.Key,
                        certificateVerificationResult);
                    break;

                case SignatureDecision.Warn:
                    _logger.LogWarning(
                        "Invalidating signature {SignatureKey} due to certificate verification result: {CertificateVerificationResult}",
                        signature.Key,
                        certificateVerificationResult);

                    InvalidateSignature(signature, certificate);

                    _telemetryService.TrackPackageSignatureMayBeInvalidatedEvent(signature);

                    break;

                case SignatureDecision.Reject:
                    _logger.LogWarning(
                        "Rejecting signature {SignatureKey} due to certificate verification result: {CertificateVerificationResult}",
                        signature.Key,
                        certificateVerificationResult);

                    InvalidateSignature(signature, certificate);

                    _telemetryService.TrackPackageSignatureShouldBeInvalidatedEvent(signature);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unknown signature decision '{decision}' for certificate verification result: {certificateVerificationResult}");
            }
        }

        private void InvalidateSignature(PackageSignature signature, EndCertificate certificate)
        {
            signature.Status = PackageSignatureStatus.Invalid;
            signature.PackageSigningState.SigningStatus = PackageSigningStatus.Invalid;

            if (certificate.Use == EndCertificateUse.Timestamping)
            {
                var affectedTimestamps = signature.TrustedTimestamps
                                                  .Where(t => t.EndCertificate.Thumbprint == certificate.Thumbprint);

                foreach (var timestamp in affectedTimestamps)
                {
                    _logger.LogWarning(
                        "Invalidating timestamp {TimestampKey} due to invalid certificate {CertificateKey}",
                        signature.Key,
                        certificate.Key);

                    timestamp.Status = TrustedTimestampStatus.Invalid;
                }
            }
        }
    }
}
