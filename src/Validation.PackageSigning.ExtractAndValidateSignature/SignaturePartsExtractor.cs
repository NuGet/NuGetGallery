// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Packaging.Signing;

namespace NuGet.Jobs.Validation.PackageSigning.ExtractAndValidateSignature
{
    public class SignaturePartsExtractor : ISignaturePartsExtractor
    {
        private readonly ICertificateStore _certificateStore;

        public SignaturePartsExtractor(ICertificateStore certificateStore)
        {
            _certificateStore = certificateStore ?? throw new ArgumentNullException(nameof(certificateStore));
        }

        public async Task ExtractAsync(ISignedPackageReader signedPackageReader, CancellationToken token)
        {
            if (!await signedPackageReader.IsSignedAsync(token))
            {
                throw new ArgumentException("The provided package reader must refer to a signed package.", nameof(signedPackageReader));
            }

            var signatures = await signedPackageReader.GetSignaturesAsync(token);

            foreach (var signature in signatures)
            {
                foreach (var certificate in GetCertificates(signature.SignedCms))
                {
                    await SaveCertificateAsync(certificate, token);
                }

                foreach (var timestamp in signature.Timestamps)
                {
                    // TODO: use the signing-certificate-v2 attribute to prune.
                    foreach (var certificate in timestamp.SignedCms.Certificates)
                    {
                        await SaveCertificateAsync(certificate, token);
                    }
                }
            }
        }

        private IEnumerable<X509Certificate2> GetCertificates(SignedCms signedCms)
        {
            // Use the signing-certificate-v2 attribute to prune the list of certificates.
            var signingCertificateV2Attribute = signedCms
                .SignerInfos[0]
                .SignedAttributes
                .FirstOrDefault(Oids.SigningCertificateV2);

            if (signingCertificateV2Attribute == null)
            {
                throw new ArgumentException(
                    $"The first element of {nameof(SignedCms.SignerInfos)} {nameof(SignedCms)} must have a signing certificate attribute.",
                    nameof(signedCms));
            }
            
            var signingCertificateHashes = new HashSet<Hash>(AttributeUtility
                .GetESSCertIDv2Entries(signingCertificateV2Attribute)
                .Select(pair => new Hash(pair.Key, pair.Value)));

            // Try all of the candidate hash algorithms against the set of certificates found in the SignedCMS.
            var algorithmsToTry = signingCertificateHashes
                .Select(x => x.AlgorithmName)
                .Distinct();
            foreach (var algorithm in algorithmsToTry)
            {
                foreach (var certificate in signedCms.Certificates)
                {
                    var digest = CryptoHashUtility.ComputeHash(algorithm, certificate.RawData);
                    var hash = new Hash(algorithm, digest);

                    if (signingCertificateHashes.Contains(hash))
                    {
                        yield return certificate;
                    }
                }
            }
        }

        private async Task SaveCertificateAsync(X509Certificate2 certificate, CancellationToken token)
        {
            var thumbprint = certificate.ComputeSHA256Thumbprint();

            if (await _certificateStore.ExistsAsync(thumbprint, token))
            {
                return;
            }

            await _certificateStore.SaveAsync(certificate, token);
        }
    }
}
