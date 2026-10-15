// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation.PackageSigning.Configuration;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.Telemetry;
using NuGet.Services.Entities;
using NuGetGallery;

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    public class DurableIdentityValueService : IDurableIdentityValueService
    {
        private readonly IEntitiesContext _entitiesContext;
        private readonly IFeatureFlagService _featureFlagService;
        private readonly IOptionsSnapshot<ProcessSignatureConfiguration> _configuration;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<DurableIdentityValueService> _logger;

        public DurableIdentityValueService(
            IEntitiesContext entitiesContext,
            IFeatureFlagService featureFlagService,
            IOptionsSnapshot<ProcessSignatureConfiguration> configuration,
            ITelemetryService telemetryService,
            ILogger<DurableIdentityValueService> logger)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ProcessAsync(
            SignatureValidationMessage message,
            PackageRegistration packageRegistration,
            X509Certificate2 signingCertificate,
            string signingThumbprint,
            string durableIdentityValue,
            DateTimeOffset signatureTimestamp)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (packageRegistration == null)
            {
                throw new ArgumentNullException(nameof(packageRegistration));
            }

            if (signingCertificate == null)
            {
                throw new ArgumentNullException(nameof(signingCertificate));
            }

            if (string.IsNullOrEmpty(signingThumbprint))
            {
                throw new ArgumentException("The value must not be null or empty.", nameof(signingThumbprint));
            }

            if (string.IsNullOrEmpty(durableIdentityValue))
            {
                throw new ArgumentException("The value must not be null or empty.", nameof(durableIdentityValue));
            }

            var configuration = _configuration.Value;
            var record = await GetOrAddDurableIdentityValueAsync(durableIdentityValue, signingCertificate, configuration.MaxCertificateStringLength);
            await LinkCertificateAsync(record, signingCertificate, signingThumbprint);

            var linkedAccountCount = 0;
            var signatureAge = DateTimeOffset.UtcNow - signatureTimestamp;

            foreach (var account in packageRegistration.GetSigningAccounts())
            {
                if (!CanLink(account, record, signingThumbprint, signatureAge, configuration.ArtifactSigning.LinkWindow))
                {
                    continue;
                }

                account.UserDurableIdentityValues.Add(new UserDurableIdentityValue
                {
                    DurableIdentityValue = record,
                    User = account,
                    UserKey = account.Key,
                });
                linkedAccountCount++;

                _logger.LogInformation(
                    "Linked durable identity value {DurableIdentityValue} to account {AccountKey} for package {PackageId} {PackageVersion} and validation {ValidationId}.",
                    durableIdentityValue,
                    account.Key,
                    message.PackageId,
                    message.PackageVersion,
                    message.ValidationId);
            }

            await _entitiesContext.SaveChangesAsync();

            if (linkedAccountCount > 0)
            {
                _telemetryService.TrackDurableIdentityValueLinked(
                    message.PackageId,
                    message.PackageVersion,
                    message.ValidationId,
                    linkedAccountCount);
            }
        }

        private bool CanLink(
            User account,
            DurableIdentityValue record,
            string signingThumbprint,
            TimeSpan signatureAge,
            TimeSpan linkWindow)
        {
            if (account.UserDurableIdentityValues.Any(ud => ud.DurableIdentityValue == record
                || string.Equals(ud.DurableIdentityValue?.Value, record.Value, StringComparison.Ordinal)))
            {
                return false;
            }

            if (!account.UserCertificates.Any(uc => string.Equals(uc.Certificate.Thumbprint, signingThumbprint, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            if (!_featureFlagService.IsArtifactSigningDurableIdentityEnabled(account))
            {
                return false;
            }

            if (signatureAge > linkWindow)
            {
                _logger.LogInformation(
                    "Not linking durable identity value {DurableIdentityValue} to account {AccountKey} because the signature is {SignatureAge} old.",
                    record.Value,
                    account.Key,
                    signatureAge);
                return false;
            }

            return true;
        }

        private async Task<DurableIdentityValue> GetOrAddDurableIdentityValueAsync(
            string durableIdentityValue,
            X509Certificate2 signingCertificate,
            int maxLength)
        {
            var record = await _entitiesContext
                .DurableIdentityValues
                .Where(d => d.Value == durableIdentityValue)
                .FirstOrDefaultAsync();

            if (record == null)
            {
                record = new DurableIdentityValue { Value = durableIdentityValue };
                _entitiesContext.DurableIdentityValues.Add(record);
            }

            record.Subject = SignaturePartsExtractor.NoLongerThanOrNull(signingCertificate.Subject, maxLength);
            record.Issuer = SignaturePartsExtractor.NoLongerThanOrNull(signingCertificate.Issuer, maxLength);
            record.ShortSubject = SignaturePartsExtractor.NoLongerThanOrNull(signingCertificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false), maxLength);
            record.ShortIssuer = SignaturePartsExtractor.NoLongerThanOrNull(signingCertificate.GetNameInfo(X509NameType.SimpleName, forIssuer: true), maxLength);

            return record;
        }

        private async Task LinkCertificateAsync(
            DurableIdentityValue record,
            X509Certificate2 signingCertificate,
            string signingThumbprint)
        {
            var certificate = await _entitiesContext
                .Certificates
                .Where(c => c.Thumbprint == signingThumbprint)
                .FirstOrDefaultAsync();

            if (certificate == null)
            {
                certificate = new Certificate
                {
#pragma warning disable CS0618 // Only set the SHA1 thumbprint because the column is required. Never read it.
                    // CodeQL [SM02196] Only set the SHA1 thumbprint, for backwards compatibility. Never read it.
                    Sha1Thumbprint = signingCertificate.Thumbprint.ToLowerInvariant(),
#pragma warning restore CS0618
                    Thumbprint = signingThumbprint,
                };
                _entitiesContext.Certificates.Add(certificate);
            }

            if (certificate.DurableIdentityValue == null && !certificate.DurableIdentityValueKey.HasValue)
            {
                certificate.DurableIdentityValue = record;
            }
        }
    }
}
