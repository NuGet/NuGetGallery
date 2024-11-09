// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1;
using NuGet.Packaging.Signing;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages
{
    public sealed class CertificateAuthority : HttpResponder
    {
        private readonly Dictionary<string, X509Certificate2> _issuedCertificates;
        private readonly Dictionary<string, RevokedInfo> _revokedCertificates;
        private readonly Lazy<OcspResponder> _ocspResponder;
        private readonly HashSet<BigInteger> _assignedSerialNumbers;
        private byte[] _dnHash;
        private byte[] _keyHash;

        /// <summary>
        /// This base URI is shared amongst all HTTP responders hosted by the same web host instance.
        /// </summary>
        public Uri SharedUri { get; }

        public X509Certificate2 Certificate { get; }

        /// <summary>
        /// Gets the base URI specific to this HTTP responder.
        /// </summary>
        public override Uri Url { get; }

        public OcspResponder OcspResponder => _ocspResponder.Value;
        public CertificateAuthority Parent { get; }

        public Uri CertificateUri { get; }
        public Uri OcspResponderUri { get; }
        internal RSA KeyPair { get; }

        private CertificateAuthority(
            X509Certificate2 certificate,
            RSA keyPair,
            Uri sharedUri,
            CertificateAuthority parentCa)
        {
            Certificate = certificate;
            KeyPair = keyPair;
            SharedUri = sharedUri;
            Url = GenerateRandomUri();
            string fingerprint = CertificateUtilities.GenerateFingerprint(certificate);
            CertificateUri = new Uri(Url, $"{fingerprint}.cer");
            OcspResponderUri = GenerateRandomUri();
            Parent = parentCa;
            _assignedSerialNumbers = new HashSet<BigInteger>();
            // The key is the certificate serial number in hexadecimal; therefore, lookups should be case insensitive.
            _issuedCertificates = new Dictionary<string, X509Certificate2>(StringComparer.InvariantCultureIgnoreCase);
            _revokedCertificates = new Dictionary<string, RevokedInfo>(StringComparer.InvariantCultureIgnoreCase);
            _ocspResponder = new Lazy<OcspResponder>(() => OcspResponder.Create(this));
        }

        public X509Certificate2 IssueCertificate(IssueCertificateOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            Action<CertificateRequest> customizeCertificate = certificateRequest =>
                {
                    certificateRequest.CertificateExtensions.Add(new X509AuthorityInformationAccessExtension(OcspResponderUri, CertificateUri));
                    certificateRequest.CertificateExtensions.Add(
                        X509AuthorityKeyIdentifierExtension.CreateFromCertificate(
                            Certificate,
                            includeKeyIdentifier: true,
                            includeIssuerAndSerial: true));
                    certificateRequest.CertificateExtensions.Add(
                        new X509SubjectKeyIdentifierExtension(certificateRequest.PublicKey, critical: false));
                    certificateRequest.CertificateExtensions.Add(
                        new X509BasicConstraintsExtension(
                            certificateAuthority: false,
                            hasPathLengthConstraint: false,
                            pathLengthConstraint: 0,
                            critical: true));
                };

            return IssueCertificate(options, customizeCertificate);
        }

        public CertificateAuthority CreateIntermediateCertificateAuthority(IssueCertificateOptions options = null)
        {
            options = options ?? IssueCertificateOptions.CreateDefaultForIntermediateCertificateAuthority();

            Action<CertificateRequest> customizeCertificate = certificateRequest =>
                {
                    certificateRequest.CertificateExtensions.Add(new X509AuthorityInformationAccessExtension(OcspResponderUri, CertificateUri));
                    certificateRequest.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(Certificate, includeKeyIdentifier: true, includeIssuerAndSerial: true));
                    certificateRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(certificateRequest.PublicKey, critical: false));
                    certificateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
                };

            X509Certificate2 certificate = IssueCertificate(options, customizeCertificate);

            return new CertificateAuthority(certificate, options.KeyPair, SharedUri, parentCa: this);
        }

        public void Revoke(X509Certificate2 certificate, X509RevocationReason reason, DateTimeOffset revocationDate)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            if (!_issuedCertificates.ContainsKey(certificate.SerialNumber))
            {
                throw new ArgumentException("Unknown serial number.", nameof(certificate));
            }

            if (_revokedCertificates.ContainsKey(certificate.SerialNumber))
            {
                throw new ArgumentException("Certificate already revoked.", nameof(certificate));
            }

            _revokedCertificates.Add(
                certificate.SerialNumber,
                new RevokedInfo(revocationDate, reason));
        }

        public override void Respond(HttpListenerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (IsGet(context.Request) &&
                string.Equals(context.Request.RawUrl, CertificateUri.AbsolutePath, StringComparison.OrdinalIgnoreCase))
            {
                WriteResponseBody(context.Response, Certificate.RawData);
            }
            else
            {
                context.Response.StatusCode = 404;
            }
        }

        public static CertificateAuthority Create(Uri sharedUri, IssueCertificateOptions options = null)
        {
            if (sharedUri == null)
            {
                throw new ArgumentNullException(nameof(sharedUri));
            }

            if (!sharedUri.AbsoluteUri.EndsWith("/"))
            {
                sharedUri = new Uri($"{sharedUri.AbsoluteUri}/");
            }

            options = options ?? IssueCertificateOptions.CreateDefaultForRootCertificateAuthority();

            Action<CertificateRequest> customizeCertificate = certificateRequest =>
                {
                    certificateRequest.CertificateExtensions.Add(
                        new X509SubjectKeyIdentifierExtension(certificateRequest.PublicKey, critical: false));
                    certificateRequest.CertificateExtensions.Add(
                        new X509BasicConstraintsExtension(
                            certificateAuthority: true,
                            hasPathLengthConstraint: false,
                            pathLengthConstraint: 0,
                            critical: true));
                    certificateRequest.CertificateExtensions.Add(
                        new X509KeyUsageExtension(
                            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                            critical: true));
                };

            byte[] serialNumber = CertificateUtilities.GenerateSerialNumber();

            X509Certificate2 certificate = CreateCertificate(
                options.KeyPair,
                options.KeyPair,
                serialNumber,
                options.SubjectName,
                options.SubjectName,
                options.NotBefore,
                options.NotAfter,
                options.CustomizeCertificate ?? customizeCertificate);

            return new CertificateAuthority(certificate, options.KeyPair, sharedUri, parentCa: null);
        }

        internal CertStatus GetCertStatus(CertId certId, out X509Certificate2 certificate)
        {
            certificate = null;

            Oid hashAlgorithmOid = certId.AlgorithmIdentifier.Algorithm;

            if (hashAlgorithmOid.Value == Oids.Sha1)
            {
                // SHA-1 is obsolete.  We'll rely on serial number only for matching.
            }
            else if (IsSupportedHashAlgorithm(hashAlgorithmOid))
            {
                if (_dnHash is null || _keyHash is null)
                {
                    using (HashAlgorithm hashAlgorithm = CreateHashAlgorithm(hashAlgorithmOid))
                    {
                        _dnHash = hashAlgorithm.ComputeHash(Certificate.SubjectName.RawData);
                        _keyHash = hashAlgorithm.ComputeHash(Certificate.GetPublicKey());
                    }
                }

                if (!certId.IssuerNameHash.Span.SequenceEqual(_dnHash))
                {
                    return CertStatus.FromUnknown();
                }

                if (!certId.IssuerKeyHash.Span.SequenceEqual(_keyHash))
                {
                    return CertStatus.FromUnknown();
                }
            }
            else
            {
                return CertStatus.FromUnknown();
            }

            byte[] serialNumberBytes = certId.SerialNumber.ToByteArray();

            // Convert to big-endian.
            Array.Reverse(serialNumberBytes);

            string serialNumber = BitConverter.ToString(serialNumberBytes).Replace("-", "");

            if (_issuedCertificates.TryGetValue(serialNumber, out X509Certificate2 issuedCertificate))
            {
                certificate = issuedCertificate;
            }
            else
            {
                return CertStatus.FromUnknown();
            }

            if (_revokedCertificates.Count == 0)
            {
                return CertStatus.FromGood();
            }

            if (_revokedCertificates.TryGetValue(serialNumber, out RevokedInfo revokedInfo))
            {
                return CertStatus.FromRevoked(revokedInfo);
            }

            return CertStatus.FromGood();
        }

        internal Uri GenerateRandomUri()
        {
            using (var provider = RandomNumberGenerator.Create())
            {
                var bytes = new byte[32];

                provider.GetBytes(bytes);

                var path = BitConverter.ToString(bytes).Replace("-", "");

                return new Uri(SharedUri, $"{path}/");
            }
        }

        private X509Certificate2 IssueCertificate(
            IssueCertificateOptions options,
            Action<CertificateRequest> customizeCertificate)
        {
            byte[] serialNumber = CertificateUtilities.GenerateSerialNumber(_assignedSerialNumbers);
            DateTimeOffset notAfter = options.NotAfter;
            DateTimeOffset issuerNotAfter = DateTime.SpecifyKind(Certificate.NotAfter, DateTimeKind.Local);

            // An issued certificate should not have a validity period beyond the issuer's validity period.
            if (notAfter > issuerNotAfter)
            {
                notAfter = issuerNotAfter;
            }

            X509Certificate2 certificate = CreateCertificate(
                options.KeyPair,
                KeyPair,
                serialNumber,
                Certificate.SubjectName,
                options.SubjectName,
                options.NotBefore,
                notAfter,
                options.CustomizeCertificate ?? customizeCertificate);

            _issuedCertificates.Add(certificate.SerialNumber, certificate);

            return certificate;
        }

        private static X509Certificate2 CreateCertificate(
            RSA certificateKeyPair,
            RSA issuingCertificateKeyPair,
            byte[] serialNumber,
            X500DistinguishedName issuerName,
            X500DistinguishedName subjectName,
            DateTimeOffset notBefore,
            DateTimeOffset notAfter,
            Action<CertificateRequest> customizeCertificate)
        {
            CertificateRequest certificateRequest = new(subjectName, certificateKeyPair, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            customizeCertificate(certificateRequest);

            X509SignatureGenerator signatureGenerator = X509SignatureGenerator.CreateForRSA(issuingCertificateKeyPair, RSASignaturePadding.Pkcs1);

            using (X509Certificate2 certificate = certificateRequest.Create(
                issuerName,
                signatureGenerator,
                notBefore,
                notAfter,
                serialNumber))
            {
                return CertificateUtilities.GetCertificateWithPrivateKey(certificate, certificateKeyPair);
            }
        }

        private sealed class RevocationInfo
        {
            internal string SerialNumber { get; }
            internal DateTimeOffset RevocationDate { get; }
            internal RevocationReason Reason { get; }

            internal RevocationInfo(string serialNumber, DateTimeOffset revocationDate, RevocationReason reason)
            {
                SerialNumber = serialNumber;
                RevocationDate = revocationDate;
                Reason = reason;
            }
        }

        private static bool IsSupportedHashAlgorithm(Oid oid)
        {
            return oid.Value switch
            {
                Oids.Sha256 or Oids.Sha384 or Oids.Sha512 => true,
                _ => false,
            };
        }

        private static HashAlgorithm CreateHashAlgorithm(Oid oid)
        {
            return oid.Value switch
            {
                Oids.Sha256 => SHA256.Create(),
                Oids.Sha384 => SHA384.Create(),
                Oids.Sha512 => SHA512.Create(),
                _ => throw new ArgumentException($"Hash algorithm {oid.Value} is unsupported."),
            };
        }
    }
}
