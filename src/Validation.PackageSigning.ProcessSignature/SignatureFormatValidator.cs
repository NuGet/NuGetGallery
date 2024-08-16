// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NuGet.Common;
using NuGet.Jobs.Validation.PackageSigning.Configuration;
using NuGet.Packaging.Signing;

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    public class SignatureFormatValidator : ISignatureFormatValidator
    {
        private static readonly SignedPackageVerifierSettings _minimalSettings = new SignedPackageVerifierSettings(
            allowUnsigned: true,
            allowIllegal: false,
            allowUntrusted: false, // Invalid format of the signature uses this flag to determine success.
            allowIgnoreTimestamp: true,
            allowMultipleTimestamps: true,
            allowNoTimestamp: true,
            allowUnknownRevocation: true,
            reportUnknownRevocation: false,
            verificationTarget: VerificationTarget.All,
            signaturePlacement: SignaturePlacement.PrimarySignature,
            repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
            revocationMode: RevocationMode.Online);

        private static readonly PackageSignatureVerifier _minimalVerifier = new PackageSignatureVerifier(new[]
        {
            new MinimalSignatureVerificationProvider(),
        });

        private static readonly PackageSignatureVerifier _fullVerifier = new PackageSignatureVerifier(new ISignatureVerificationProvider[]
        {
            new IntegrityVerificationProvider(),
            new SignatureTrustAndValidityVerificationProvider(),
            new AllowListVerificationProvider(allowList: null),
        });

        private readonly IOptionsSnapshot<ProcessSignatureConfiguration> _config;
        private readonly SignedPackageVerifierSettings _authorSignatureSettings;
        private readonly SignedPackageVerifierSettings _repositorySignatureSettings;
        private readonly SignedPackageVerifierSettings _authorOrRepositorySignatureSettings;

        public SignatureFormatValidator(IOptionsSnapshot<ProcessSignatureConfiguration> config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _authorSignatureSettings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                verificationTarget: VerificationTarget.Author,
                signaturePlacement: SignaturePlacement.PrimarySignature,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Never,
                revocationMode: RevocationMode.Online);

            var repoAllowListEntries = _config
                .Value
                .AllowedRepositorySigningCertificates?
                .Select(hash => new CertificateHashAllowListEntry(
                    VerificationTarget.Repository,
                    SignaturePlacement.PrimarySignature | SignaturePlacement.Countersignature,
                    hash,
                    HashAlgorithmName.SHA256))
                .ToList();

            repoAllowListEntries = repoAllowListEntries ?? new List<CertificateHashAllowListEntry>();

            _repositorySignatureSettings = new SignedPackageVerifierSettings(
                allowUnsigned: _authorSignatureSettings.AllowUnsigned,
                allowIllegal: _authorSignatureSettings.AllowIllegal,
                allowUntrusted: _authorSignatureSettings.AllowUntrusted,
                allowIgnoreTimestamp: _authorSignatureSettings.AllowIgnoreTimestamp,
                allowMultipleTimestamps: _authorSignatureSettings.AllowMultipleTimestamps,
                allowNoTimestamp: _authorSignatureSettings.AllowNoTimestamp,
                allowUnknownRevocation: _authorSignatureSettings.AllowUnknownRevocation,
                reportUnknownRevocation: _authorSignatureSettings.ReportUnknownRevocation,
                verificationTarget: VerificationTarget.Repository,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExists,
                revocationMode: _authorSignatureSettings.RevocationMode);

            _authorOrRepositorySignatureSettings = new SignedPackageVerifierSettings(
                allowUnsigned: _authorSignatureSettings.AllowUnsigned,
                allowIllegal: _authorSignatureSettings.AllowIllegal,
                allowUntrusted: _authorSignatureSettings.AllowUntrusted,
                allowIgnoreTimestamp: _authorSignatureSettings.AllowIgnoreTimestamp,
                allowMultipleTimestamps: _authorSignatureSettings.AllowMultipleTimestamps,
                allowNoTimestamp: _authorSignatureSettings.AllowNoTimestamp,
                allowUnknownRevocation: _authorSignatureSettings.AllowUnknownRevocation,
                reportUnknownRevocation: _authorSignatureSettings.ReportUnknownRevocation,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExists,
                revocationMode: _authorSignatureSettings.RevocationMode);
        }

        public async Task<VerifySignaturesResult> ValidateMinimalAsync(
            ISignedPackageReader package,
            CancellationToken token)
        {
            return await _minimalVerifier.VerifySignaturesAsync(
                package,
                _minimalSettings,
                token);
        }

        public async Task<VerifySignaturesResult> ValidateAuthorSignatureAsync(
            ISignedPackageReader package,
            CancellationToken token)
        {
            return await _fullVerifier.VerifySignaturesAsync(
                package,
                _authorSignatureSettings,
                token);
        }

        public async Task<VerifySignaturesResult> ValidateRepositorySignatureAsync(
            ISignedPackageReader package,
            CancellationToken token)
        {
            return await _fullVerifier.VerifySignaturesAsync(
                package,
                _repositorySignatureSettings,
                token);
        }

        public async Task<VerifySignaturesResult> ValidateAllSignaturesAsync(
            ISignedPackageReader package,
            bool hasRepositorySignature,
            CancellationToken token)
        {
            // TODO - Use only the "authorOrRepositorySignatureSettings" once this issue is fixed:
            // https://github.com/NuGet/Home/issues/7042
            var settings = hasRepositorySignature ? _authorOrRepositorySignatureSettings : _authorSignatureSettings;

            return await _fullVerifier.VerifySignaturesAsync(
                package,
                settings,
                token);
        }
    }
}
