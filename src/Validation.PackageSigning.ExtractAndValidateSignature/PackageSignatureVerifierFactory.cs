// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging.Signing;

namespace NuGet.Jobs.Validation.PackageSigning.ExtractAndValidateSignature
{
    /// <summary>
    /// Initializes integration with the client APIs, which handles the actual signature verification.
    /// </summary>
    public static class PackageSignatureVerifierFactory
    {
        /// <summary>
        /// Initializes a verifier that only verifies the format of the signature. No integrity or trust checks are
        /// performed.
        /// </summary>
        public static IPackageSignatureVerifier CreateMinimal()
        {
            var verificationProviders = new[]
            {
                new MinimalSignatureVerificationProvider(),
            };

            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: true,
                allowUntrusted: false, // Invalid format of the signature uses this flag to determine success.
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true);

            return new PackageSignatureVerifier(
                verificationProviders,
                settings);
        }

        /// <summary>
        /// Initializes a verifier that performs all integrity and trust checks required by the server.
        /// </summary>
        public static IPackageSignatureVerifier CreateFull()
        {
            var verificationProviders = new ISignatureVerificationProvider[]
            {
                new IntegrityVerificationProvider(),
                new SignatureTrustAndValidityVerificationProvider(),
            };

            var settings = new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: false,
                allowNoTimestamp: false,
                allowUnknownRevocation: true);

            return new PackageSignatureVerifier(
                verificationProviders,
                settings);
        }
    }
}
