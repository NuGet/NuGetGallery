// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NuGet.Common;
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
            allowNoRepositoryCertificateList: true,
            allowNoClientCertificateList: true,
            alwaysVerifyCountersignature: false,
            repoAllowListEntries: null,
            clientAllowListEntries: null);

        private static readonly IEnumerable<ISignatureVerificationProvider> _minimalProviders = new[]
        {
            new MinimalSignatureVerificationProvider(),
        };

        private static readonly IEnumerable<ISignatureVerificationProvider> _fullProviders = new ISignatureVerificationProvider[]
        {
            new IntegrityVerificationProvider(),
            new SignatureTrustAndValidityVerificationProvider(),
            new AllowListVerificationProvider(),
        };

        private readonly IOptionsSnapshot<ProcessSignatureConfiguration> _config;
        private readonly SignedPackageVerifierSettings _authorSignatureSettings;
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
                allowNoClientCertificateList: true,
                alwaysVerifyCountersignature: true,
                clientAllowListEntries: null,
                allowNoRepositoryCertificateList: true,
                repoAllowListEntries: null);

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

            _authorOrRepositorySignatureSettings = new SignedPackageVerifierSettings(
                allowUnsigned: _authorSignatureSettings.AllowUnsigned,
                allowIllegal: _authorSignatureSettings.AllowIllegal,
                allowUntrusted: _authorSignatureSettings.AllowUntrusted,
                allowIgnoreTimestamp: _authorSignatureSettings.AllowIgnoreTimestamp,
                allowMultipleTimestamps: _authorSignatureSettings.AllowMultipleTimestamps,
                allowNoTimestamp: _authorSignatureSettings.AllowNoTimestamp,
                allowUnknownRevocation: _authorSignatureSettings.AllowUnknownRevocation,
                allowNoClientCertificateList: _authorSignatureSettings.AllowNoClientCertificateList,
                alwaysVerifyCountersignature: _authorSignatureSettings.AlwaysVerifyCountersignature,
                clientAllowListEntries: _authorSignatureSettings.ClientCertificateList,
                allowNoRepositoryCertificateList: false,
                repoAllowListEntries: repoAllowListEntries);
        }

        public async Task<VerifySignaturesResult> ValidateMinimalAsync(
            ISignedPackageReader package,
            CancellationToken token)
        {
            return await VerifyAsync(
                package,
                _minimalProviders,
                _minimalSettings,
                token);
        }

        public async Task<VerifySignaturesResult> ValidateFullAsync(
            ISignedPackageReader package,
            bool hasRepositorySignature,
            CancellationToken token)
        {
            var settings = hasRepositorySignature ? _authorOrRepositorySignatureSettings : _authorSignatureSettings;

            return await VerifyAsync(
                package,
                _fullProviders,
                settings,
                token);
        }

        private static async Task<VerifySignaturesResult> VerifyAsync(
            ISignedPackageReader package,
            IEnumerable<ISignatureVerificationProvider> verificationProviders,
            SignedPackageVerifierSettings settings,
            CancellationToken token)
        {
            var verifier = new PackageSignatureVerifier(verificationProviders);

            return await verifier.VerifySignaturesAsync(
                package,
                settings,
                token);
        }
    }
}
