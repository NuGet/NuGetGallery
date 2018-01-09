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
        public static IPackageSignatureVerifier Create()
        {
            var verificationProviders = new[]
            {
                new IntegrityVerificationProvider(),
            };

            var settings = SignedPackageVerifierSettings.VerifyCommandDefaultPolicy;

            return new PackageSignatureVerifier(
                verificationProviders,
                settings);
        }
    }
}
