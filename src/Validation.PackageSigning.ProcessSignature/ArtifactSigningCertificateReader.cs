// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation.PackageSigning.Configuration;
using NuGet.Packaging.Signing;

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    public class ArtifactSigningCertificateReader : IArtifactSigningCertificateReader
    {
        private readonly IOptionsSnapshot<ProcessSignatureConfiguration> _configuration;
        private readonly ILogger<ArtifactSigningCertificateReader> _logger;

        public ArtifactSigningCertificateReader(
            IOptionsSnapshot<ProcessSignatureConfiguration> configuration,
            ILogger<ArtifactSigningCertificateReader> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool TryGetDurableIdentityValue(PrimarySignature signature, out string durableIdentityValue)
        {
            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            try
            {
                using (var certificateChain = SignatureUtility.GetCertificateChain(signature))
                {
                    return TryGetDurableIdentityValue(certificateChain.ToList(), out durableIdentityValue);
                }
            }
            catch (Exception ex) when (ex is SignatureException || ex is CryptographicException)
            {
                _logger.LogWarning(0, ex, "Unable to read the certificate chain of the signature to find a durable identity value.");
                durableIdentityValue = null;
                return false;
            }
        }

        internal bool TryGetDurableIdentityValue(IReadOnlyList<X509Certificate2> certificateChain, out string durableIdentityValue)
        {
            durableIdentityValue = null;

            if (certificateChain == null || certificateChain.Count < 2)
            {
                return false;
            }

            var configuration = _configuration.Value.ArtifactSigning;
            var leafEkus = GetEnhancedKeyUsageOids(certificateChain[0]);

            if (!leafEkus.Contains(configuration.ArtifactSigningEkuOid, StringComparer.Ordinal))
            {
                return false;
            }

            var candidates = leafEkus
                .Where(oid => oid != configuration.ArtifactSigningEkuOid
                    && oid.StartsWith(configuration.DurableIdentityValueOidPrefix, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (candidates.Count != 1)
            {
                _logger.LogWarning(
                    "Artifact Signing certificate {Thumbprint} has {Count} durable identity value EKUs instead of exactly one.",
                    certificateChain[0].Thumbprint,
                    candidates.Count);
                return false;
            }

            var policyThumbprints = configuration.GetPolicyCertificateThumbprints();
            var chainsToPolicyCertificate = certificateChain
                .Skip(1)
                .Any(certificate => policyThumbprints.Contains(certificate.ComputeSHA256Thumbprint(), StringComparer.OrdinalIgnoreCase));

            if (!chainsToPolicyCertificate)
            {
                _logger.LogWarning(
                    "Artifact Signing certificate {Thumbprint} does not chain to a configured policy certificate.",
                    certificateChain[0].Thumbprint);
                return false;
            }

            durableIdentityValue = candidates[0];
            return true;
        }

        private static IReadOnlyList<string> GetEnhancedKeyUsageOids(X509Certificate2 certificate)
        {
            var extension = certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().FirstOrDefault();

            if (extension == null)
            {
                return Array.Empty<string>();
            }

            return extension.EnhancedKeyUsages.Cast<Oid>().Select(oid => oid.Value).ToList();
        }
    }
}
