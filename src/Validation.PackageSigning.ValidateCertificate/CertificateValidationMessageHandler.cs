// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation;

namespace Validation.PackageSigning.ValidateCertificate
{
    /// <summary>
    /// The handler for <see cref="CertificateValidationMessage"/>. Upon receiving a message,
    /// this will validate a <see cref="X509Certificate2"/> and perform online revocation checks.
    /// </summary>
    public sealed class CertificateValidationMessageHandler : IMessageHandler<CertificateValidationMessage>
    {
        private readonly ICertificateStore _certificateStore;
        private readonly ICertificateValidationService _certificateValidationService;
        private readonly ICertificateVerifier _certificateVerifier;
        private readonly IPackageValidationEnqueuer _validationEnqueuer;
        private readonly IFeatureFlagService _featureFlagService;
        private readonly ILogger<CertificateValidationMessageHandler> _logger;

        private readonly int _maximumValidationFailures;

        public CertificateValidationMessageHandler(
            ICertificateStore certificateStore,
            ICertificateValidationService certificateValidationService,
            ICertificateVerifier certificateVerifier,
            IPackageValidationEnqueuer validationEnqueuer,
            IFeatureFlagService featureFlagService,
            ILogger<CertificateValidationMessageHandler> logger,
            int maximumValidationFailures = CertificateValidationService.DefaultMaximumValidationFailures)
        {
            _certificateStore = certificateStore ?? throw new ArgumentNullException(nameof(certificateStore));
            _certificateValidationService = certificateValidationService ?? throw new ArgumentNullException(nameof(certificateValidationService));
            _certificateVerifier = certificateVerifier ?? throw new ArgumentNullException(nameof(certificateVerifier));
            _validationEnqueuer = validationEnqueuer ?? throw new ArgumentNullException(nameof(validationEnqueuer));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _maximumValidationFailures = maximumValidationFailures;
        }

        /// <summary>
        /// Perform the certificate validation request, including online revocation checks.
        /// </summary>
        /// <param name="message">The message requesting the certificate validation.</param>
        /// <returns>Whether the validation completed. If false, the validation should be retried later.</returns>
        public async Task<bool> HandleAsync(CertificateValidationMessage message)
        {
            using (_logger.BeginScope("Handling validate certificate message {CertificateKey} {ValidationId}",
                message.CertificateKey,
                message.ValidationId))
            {
                var validation = await _certificateValidationService.FindCertificateValidationAsync(message);

                if (validation == null)
                {
                    _logger.LogInformation(
                        "Could not find a certificate validation entity, failing (certificate: {CertificateKey} validation: {ValidationId})",
                        message.CertificateKey,
                        message.ValidationId);

                    return false;
                }

                if (validation.Status != null)
                {
                    // A certificate validation should be queued with a Status of null, and once the certificate validation
                    // completes, the Status should be updated to a non-null value. Hence, the Status here SHOULD be null.
                    // A non-null Status may indicate message duplication.
                    _logger.LogWarning(
                        "Invalid certificate validation entity's status, dropping message (certificate: {CertificateThumbprint} validation: {ValidationId})",
                        validation.EndCertificate.Thumbprint,
                        validation.ValidationId);

                    return true;
                }

                if (validation.EndCertificate.Status == EndCertificateStatus.Revoked)
                {
                    if (message.RevalidateRevokedCertificate)
                    {
                        _logger.LogWarning(
                            "Revalidating certificate that is known to be revoked " +
                            "(certificate: {CertificateThumbprint} validation: {ValidationId})",
                            validation.EndCertificate.Thumbprint,
                            validation.ValidationId);
                    }
                    else
                    {
                        // Do NOT revalidate a certificate that is known to be revoked unless explicitly told to!
                        // Certificate Authorities are not required to keep a certificate's revocation information
                        // forever, therefore, revoked certificates should only be revalidated in special cases.
                        _logger.LogError(
                            "Certificate known to be revoked MUST be validated with the " +
                            $"{nameof(CertificateValidationMessage.RevalidateRevokedCertificate)} flag enabled " +
                            "(certificate: {CertificateThumbprint} validation: {ValidationId})",
                            validation.EndCertificate.Thumbprint,
                            validation.ValidationId);

                        return true;
                    }
                }

                CertificateVerificationResult result;

                using (var certificates = await LoadCertificatesAsync(validation))
                {
                    switch (validation.EndCertificate.Use)
                    {
                        case EndCertificateUse.CodeSigning:
                            result = _certificateVerifier.VerifyCodeSigningCertificate(
                                        certificates.EndCertificate,
                                        certificates.AncestorCertificates);
                            break;

                        case EndCertificateUse.Timestamping:
                            result = _certificateVerifier.VerifyTimestampingCertificate(
                                        certificates.EndCertificate,
                                        certificates.AncestorCertificates);
                            break;

                        default:
                            throw new InvalidOperationException($"Unknown {nameof(EndCertificateUse)}: {validation.EndCertificate.Use}");
                    }
                }

                // Save the result. This may alert if packages are invalidated.
                if (!await _certificateValidationService.TrySaveResultAsync(validation, result))
                {
                    _logger.LogWarning(
                        "Failed to save certificate validation result " +
                        "(certificate: {CertificateThumbprint} validation: {ValidationId}), " +
                        "retrying validation...",
                        validation.EndCertificate.Thumbprint,
                        validation.ValidationId);

                    return false;
                }

                var completed = HasValidationCompleted(validation, result);
                if (completed && message.SendCheckValidator && _featureFlagService.IsQueueBackEnabled())
                {
                    // The validation has completed (either a terminal success or a terminal failure). This message
                    // we are enqueueing notifies the orchestrator that this validator's work is done and means the
                    // orchestrator can continue with the rest of the validation process.
                    _logger.LogInformation("Sending queue-back message for validation {ValidationId}.", message.ValidationId);
                    var messageData = PackageValidationMessageData.NewCheckValidator(message.ValidationId);
                    await _validationEnqueuer.StartValidationAsync(messageData);
                }

                return completed;
            }
        }

        private bool HasValidationCompleted(EndCertificateValidation validation, CertificateVerificationResult result)
        {
            // The validation is complete if the certificate was determined to be "Good", "Invalid", or "Revoked".
            if (result.Status == EndCertificateStatus.Good
                || result.Status == EndCertificateStatus.Invalid
                || result.Status == EndCertificateStatus.Revoked)
            {
                return true;
            }
            else if (result.Status == EndCertificateStatus.Unknown)
            {
                // Certificates whose status failed to be determined will have an "Unknown"
                // status. These certificates should be retried until "_maximumValidationFailures"
                // is reached.
                if (validation.EndCertificate.ValidationFailures >= _maximumValidationFailures)
                {
                    _logger.LogWarning(
                        "Certificate {CertificateThumbprint} has reached maximum of {MaximumValidationFailures} failed validation attempts",
                        validation.EndCertificate.Thumbprint,
                        _maximumValidationFailures);

                    return true;
                }
                else
                {
                    _logger.LogWarning(
                        "Could not validate certificate {CertificateThumbprint}, {RetriesLeft} retries left",
                        validation.EndCertificate.Thumbprint,
                        _maximumValidationFailures - validation.EndCertificate.ValidationFailures);

                    return false;
                }
            }

            _logger.LogError(
                $"Unknown {nameof(EndCertificateStatus)} value: {{CertificateStatus}}, throwing to retry",
                result.Status);

            throw new InvalidOperationException($"Unknown {nameof(EndCertificateStatus)} value: {result.Status}");
        }

        private async Task<LoadCertificatesResult> LoadCertificatesAsync(EndCertificateValidation validation)
        {
            // Create a list of all the thumbprints that need to be downloaded. The first thumbprint is the end certificate,
            // the rest are the end certificate's ancestors.
            var thumbprints = new List<string>();

            thumbprints.Add(validation.EndCertificate.Thumbprint);
            thumbprints.AddRange(validation.EndCertificate.CertificateChainLinks.Select(l => l.ParentCertificate.Thumbprint));

            var certificates = await Task.WhenAll(thumbprints.Select(t => _certificateStore.LoadAsync(t, CancellationToken.None)));

            return new LoadCertificatesResult(
                endCertificate: certificates.First(),
                ancestorCertificates: certificates.Skip(1).ToArray());
        }

        private class LoadCertificatesResult : IDisposable
        {
            public LoadCertificatesResult(
                X509Certificate2 endCertificate,
                IReadOnlyList<X509Certificate2> ancestorCertificates)
            {
                EndCertificate = endCertificate ?? throw new ArgumentNullException(nameof(endCertificate));
                AncestorCertificates = ancestorCertificates ?? throw new ArgumentNullException(nameof(ancestorCertificates));
            }

            public X509Certificate2 EndCertificate { get; }
            public IReadOnlyList<X509Certificate2> AncestorCertificates { get; }

            public void Dispose()
            {
                EndCertificate.Dispose();

                foreach (var ancestor in AncestorCertificates)
                {
                    ancestor.Dispose();
                }
            }
        }
    }
}
