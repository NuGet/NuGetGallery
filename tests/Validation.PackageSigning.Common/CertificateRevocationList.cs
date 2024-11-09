// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages
{
    public sealed class CertificateRevocationList : IDisposable
    {
        private readonly CertificateRevocationListBuilder _crlBuilder;
        private readonly X509CertificateWithKeyInfo _issuerCert;
        private readonly string _crlFilePath;
        private BigInteger _nextVersion;

        public CertificateRevocationList(
            X509CertificateWithKeyInfo issuerCert,
            string crlLocalPath)
        {
            if (issuerCert is null)
            {
                throw new ArgumentNullException(nameof(issuerCert));
            }

            _issuerCert = issuerCert;
            _crlFilePath = Path.Combine(crlLocalPath, $"{issuerCert.Certificate.Subject}.crl");
            _nextVersion = BigInteger.One;
            _crlBuilder = new CertificateRevocationListBuilder();
        }

        public void RevokeCertificate(X509Certificate2 revokedCertificate)
        {
            _crlBuilder.AddEntry(revokedCertificate, DateTimeOffset.Now, X509RevocationReason.KeyCompromise);

            Publish();
        }

        public void Publish()
        {
            DateTimeOffset thisUpdate = DateTimeOffset.Now;
            byte[] encoded = _crlBuilder.Build(
                _issuerCert.Certificate,
                _nextVersion,
                thisUpdate.AddDays(1),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1,
                thisUpdate);

            ++_nextVersion;

            string base64 = Convert.ToBase64String(encoded);

            using (StreamWriter streamWriter = new(File.Open(_crlFilePath, FileMode.Create)))
            {
                const string label = "X509 CRL";
                streamWriter.WriteLine($"-----BEGIN {label}-----");
                streamWriter.WriteLine(base64);
                streamWriter.WriteLine($"-----END {label}-----");
            }
        }

        public void Dispose()
        {
            if (!string.IsNullOrEmpty(_crlFilePath) && File.Exists(_crlFilePath))
            {
                File.Delete(_crlFilePath);
            }
        }
    }
}
