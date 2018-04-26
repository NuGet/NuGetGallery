// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Validation;

namespace Validation.PackageSigning.RevalidateCertificate
{
    public class CertificateRevalidator : ICertificateRevalidator
    {
        private readonly RevalidationConfiguration _config;
        private readonly IValidationEntitiesContext _context;
        private readonly IValidateCertificateEnqueuer _validationEnqueuer;
        private readonly ITelemetryService _telemetry;
        private readonly ILogger<CertificateRevalidator> _logger;

        public CertificateRevalidator(
            RevalidationConfiguration config,
            IValidationEntitiesContext context,
            IValidateCertificateEnqueuer validationEnqueuer,
            ITelemetryService telemetry,
            ILogger<CertificateRevalidator> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _validationEnqueuer = validationEnqueuer ?? throw new ArgumentNullException(nameof(validationEnqueuer));
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task PromoteSignaturesAsync()
        {
            using (_telemetry.TrackPromoteSignaturesDuration())
            {
                var promotableSignatures = await FindPromotableSignaturesAsync();

                if (!promotableSignatures.Any())
                {
                    return;
                }

                foreach (var signature in promotableSignatures)
                {
                    _logger.LogInformation(
                        $"Promoting signature {{SignatureKey}} for package {{PackageKey}} to status {nameof(PackageSignatureStatus.Valid)}",
                        signature.Key,
                        signature.PackageKey);

                    signature.Status = PackageSignatureStatus.Valid;
                }

                await _context.SaveChangesAsync();
            }
        }

        private async Task<List<PackageSignature>> FindPromotableSignaturesAsync()
        {
            var promotableSignatures = new List<PackageSignature>();
            var signaturesScanned = 0;
            var scans = 0;

            while (promotableSignatures.Count < _config.SignaturePromotionBatchSize)
            {
                var take = Math.Min(
                    _config.SignaturePromotionScanSize,
                    _config.SignaturePromotionBatchSize - promotableSignatures.Count);

                var potentialSignatures = await _context.PackageSignatures
                    .Where(s => s.Status == PackageSignatureStatus.InGracePeriod)
                    .Where(s => s.Type == PackageSignatureType.Author)
                    .Include(s => s.EndCertificate)
                    .Include(s => s.TrustedTimestamps.Select(t => t.EndCertificate))
                    .OrderBy(s => s.CreatedAt)
                    .Skip(signaturesScanned)
                    .Take(take)
                    .ToListAsync();

                promotableSignatures.AddRange(potentialSignatures.Where(s => s.IsPromotable()));

                signaturesScanned += potentialSignatures.Count;
                scans += 1;

                // We've scanned all potential signatures if the last scan found less potential signatures
                // than the maximal scan size.
                if (potentialSignatures.Count < _config.SignaturePromotionScanSize)
                {
                    break;
                }
            }

            _logger.LogInformation(
                "Found {PromotableSignaturesCount} promotable signatures after {ScanCount} scans",
                promotableSignatures.Count,
                scans);

            return promotableSignatures;
        }

        public async Task RevalidateStaleCertificatesAsync()
        {
            using (_telemetry.TrackCertificateRevalidationDuration())
            {
                var certificates = await FindStaleCertificatesAsync();

                if (!certificates.Any())
                {
                    _logger.LogInformation("Did not find any stale certificates to revalidate");
                    return;
                }

                var validationId = Guid.NewGuid();
                var stopwatch = Stopwatch.StartNew();

                using (_logger.BeginScope("Starting validation {ValidationId} for {CertificateCount} certificates...",
                    validationId,
                    certificates.Count))
                {
                    await StartCertificateValidationsAsync(validationId, certificates);
                    await WaitOnCertificateValidationsAsync(validationId, stopwatch);
                }
            }
        }

        private Task<List<EndCertificate>> FindStaleCertificatesAsync()
        {
            // Find certificates that are before the stale cut off. Revoked certificates should never
            // be revalidated as Certificate Authorities may not drop revocation status.
            var staleCutoff = DateTime.UtcNow - _config.RevalidationPeriodForCertificates;

            return _context.EndCertificates
                .Where(c => c.Status != EndCertificateStatus.Revoked)
                .Where(c => c.LastVerificationTime != null)
                .Where(c => c.LastVerificationTime < staleCutoff)
                .OrderBy(c => c.LastVerificationTime)
                .Take(_config.CertificateRevalidationBatchSize)
                .ToListAsync();
        }

        private async Task StartCertificateValidationsAsync(Guid validationId, List<EndCertificate> certificates)
        {
            _logger.LogInformation("Starting {Count} certificate validations...", certificates.Count);

            var validationTasks = new List<Task>();

            foreach (var certificate in certificates)
            {
                var task = _validationEnqueuer.EnqueueValidationAsync(validationId, certificate);

                validationTasks.Add(task);

                _context.CertificateValidations.Add(new EndCertificateValidation
                {
                    ValidationId = validationId,
                    EndCertificate = certificate,
                    Status = null,
                });
            }

            // Wait until all revalidations have been enqueued, then, persist database changes.
            await Task.WhenAll(validationTasks);
            await _context.SaveChangesAsync();
        }

        private async Task WaitOnCertificateValidationsAsync(Guid validationId, Stopwatch stopwatch)
        {
            _logger.LogInformation("Waiting until all certificate validations finish...");

            while (stopwatch.Elapsed < _config.CertificateRevalidationTimeout)
            {
                await Task.Delay(_config.CertificateRevalidationPollTime);

                var validationsLeft = await _context.CertificateValidations
                    .Where(v => v.ValidationId == validationId)
                    .Where(v => v.Status == null)
                    .CountAsync();

                if (validationsLeft == 0)
                {
                    _logger.LogInformation("All certificate validations finished after {ElapsedTime}", stopwatch.Elapsed);

                    return;
                }
                else if (stopwatch.Elapsed >= _config.CertificateRevalidationTrackAfter)
                {
                    _logger.LogWarning(
                        "{ValidationsLeft} certificate validations left after {ElapsedTime} - this is longer than expected!",
                        validationsLeft,
                        stopwatch.Elapsed);

                    _telemetry.TrackCertificateRevalidationTakingTooLong();
                }
                else
                {
                    _logger.LogInformation(
                        "{ValidationsLeft} certificate validations left after {ElapsedTime}...",
                        validationsLeft,
                        stopwatch.Elapsed);
                }
            }

            if (stopwatch.Elapsed >= _config.CertificateRevalidationTimeout)
            {
                _logger.LogError("Reached certificate revalidation timeout after {ElapsedTime}", stopwatch.Elapsed);
                _telemetry.TrackCertificateRevalidationReachedTimeout();
            }
        }
    }
}
