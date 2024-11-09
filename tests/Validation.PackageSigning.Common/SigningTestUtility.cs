// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using Xunit;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages
{
    public static class SigningTestUtility
    {
        private static readonly string SignatureLogPrefix = "Package '{0} {1}' from source '{2}':";

        /// <summary>
        /// Modification generator that can be passed to TestCertificate.Generate().
        /// The generator will change the certificate EKU to ClientAuth.
        /// </summary>
        public static Action<TestCertificateGenerator> CertificateModificationGeneratorForInvalidEkuCert = delegate (TestCertificateGenerator gen)
        {
            // any EKU besides CodeSigning
            var usages = new OidCollection { TestOids.ClientAuthenticationEku };

            gen.Extensions.Add(
                 new X509EnhancedKeyUsageExtension(
                     usages,
                     critical: true));
        };

        /// <summary>
        /// Modification generator that can be passed to TestCertificate.Generate().
        /// The generator will change the certificate EKU to CodeSigning.
        /// </summary>
        public static Action<TestCertificateGenerator> CertificateModificationGeneratorForCodeSigningEkuCert = delegate (TestCertificateGenerator gen)
        {
            var usages = new OidCollection { new Oid(Oids.CodeSigningEku) };

            gen.Extensions.Add(
                  new X509EnhancedKeyUsageExtension(
                      usages,
                      critical: true));
        };

        /// <summary>
        /// Modification generator that can be passed to TestCertificate.Generate().
        /// The generator will create an expired certificate.
        /// </summary>
        public static Action<TestCertificateGenerator> CertificateModificationGeneratorExpiredCert = delegate (TestCertificateGenerator gen)
        {
            var usages = new OidCollection { new Oid(Oids.CodeSigningEku) };

            gen.Extensions.Add(
                  new X509EnhancedKeyUsageExtension(
                      usages,
                      critical: true));

            gen.NotBefore = DateTime.UtcNow.AddHours(-1);
            gen.NotAfter = DateTime.UtcNow.AddMinutes(-1);
        };

        /// <summary>
        /// Modification generator that can be passed to TestCertificate.Generate().
        /// The generator will create a certificate that is not yet valid.
        /// </summary>
        public static Action<TestCertificateGenerator> CertificateModificationGeneratorNotYetValidCert = delegate (TestCertificateGenerator gen)
        {
            var usages = new OidCollection { new Oid(Oids.CodeSigningEku) };

            gen.Extensions.Add(
             new X509EnhancedKeyUsageExtension(
                 usages,
                 critical: true));

            var notBefore = DateTime.UtcNow.AddDays(1);

            gen.NotBefore = notBefore;
            gen.NotAfter = notBefore.AddHours(1);
        };

        /// <summary>
        /// Modification generator that can be passed to TestCertificate.Generate().
        /// The generator will create a certificate that is valid but will expire soon.
        /// </summary>
        public static Action<TestCertificateGenerator> CertificateModificationGeneratorForCertificateThatWillExpireSoon(TimeSpan expiresIn)
        {
            if (expiresIn < TimeSpan.Zero)
            {
                throw new ArgumentException("The value must not be negative.", nameof(expiresIn));
            }

            return (TestCertificateGenerator gen) =>
            {
                var usages = new OidCollection { new Oid(Oids.CodeSigningEku) };

                gen.Extensions.Add(
                    new X509EnhancedKeyUsageExtension(
                        usages,
                        critical: true));

                gen.NotBefore = DateTime.UtcNow.AddHours(-1);
                gen.NotAfter = DateTime.UtcNow.AddSeconds(expiresIn.TotalSeconds);
            };
        }

        /// <summary>
        /// Modification generator that can be passed to TestCertificate.Generate().
        /// The generator will create a certificate that is only valid for a specified short period.
        /// </summary>
        public static Action<TestCertificateGenerator> CertificateModificationGeneratorForCertificateThatOnlyValidInSpecifiedPeriod(DateTimeOffset notBefore, DateTimeOffset notAfter)
        {
            return (TestCertificateGenerator gen) =>
            {
                var usages = new OidCollection { new Oid(Oids.CodeSigningEku) };

                gen.Extensions.Add(
                    new X509EnhancedKeyUsageExtension(
                        usages,
                        critical: true));

                gen.NotBefore = notBefore;
                gen.NotAfter = notAfter;
            };
        }
        /// <summary>
        /// Generates a list of certificates representing a chain of certificates.
        /// The first certificate is the root certificate stored in StoreName.Root and StoreLocation.LocalMachine.
        /// The last certificate is the leaf certificate stored in StoreName.TrustedPeople and StoreLocation.LocalMachine.
        /// Please dispose all the certificates in the list after use.
        /// </summary>
        /// <param name="length">Length of the chain.</param>
        /// <param name="crlServerUri">Uri for crl server</param>
        /// <param name="crlLocalUri">Uri for crl local</param>
        /// <param name="configureLeafCrl">Indicates if leaf crl should be configured</param>
        /// <param name="leafCertificateActionGenerator">Specify actionGenerator for the leaf certificate of the chain</param>
        /// <returns>List of certificates representing a chain of certificates.</returns>
        public static IList<TrustedTestCert<TestCertificate>> GenerateCertificateChain(int length, string crlServerUri, string crlLocalUri, bool configureLeafCrl = true, Action<TestCertificateGenerator> leafCertificateActionGenerator = null)
        {
            var certChain = new List<TrustedTestCert<TestCertificate>>();
            var actionGenerator = CertificateModificationGeneratorForCodeSigningEkuCert;
            var leafGenerator = leafCertificateActionGenerator ?? actionGenerator;
            TrustedTestCert<TestCertificate> issuer = null;
            TrustedTestCert<TestCertificate> cert = null;

            for (var i = 0; i < length; i++)
            {
                if (i == 0) // root CA cert
                {
                    var chainCertificateRequest = new ChainCertificateRequest()
                    {
                        ConfigureCrl = true,
                        CrlLocalBaseUri = crlLocalUri,
                        CrlServerBaseUri = crlServerUri,
                        IsCA = true
                    };

                    cert = TestCertificate.Generate(X509StorePurpose.CodeSigning, actionGenerator, chainCertificateRequest)
                        .WithPrivateKeyAndTrust(StoreName.Root);
                    issuer = cert;
                }
                else if (i < length - 1) // intermediate CA cert
                {
                    var chainCertificateRequest = new ChainCertificateRequest()
                    {
                        ConfigureCrl = true,
                        CrlLocalBaseUri = crlLocalUri,
                        CrlServerBaseUri = crlServerUri,
                        IsCA = true,
                        Issuer = issuer.Source.Cert
                    };

                    cert = TestCertificate.Generate(X509StorePurpose.CodeSigning, actionGenerator, chainCertificateRequest)
                        .WithPrivateKeyAndTrustForIntermediateCertificateAuthority();
                    issuer = cert;
                }
                else // leaf cert
                {
                    var chainCertificateRequest = new ChainCertificateRequest()
                    {
                        CrlLocalBaseUri = crlLocalUri,
                        CrlServerBaseUri = crlServerUri,
                        IsCA = false,
                        ConfigureCrl = configureLeafCrl,
                        Issuer = issuer.Source.Cert
                    };

                    cert = TestCertificate.Generate(X509StorePurpose.CodeSigning, leafGenerator, chainCertificateRequest)
                        .WithPrivateKeyAndTrustForLeafOrSelfIssued();
                }

                certChain.Add(cert);
            }

            return certChain;
        }

        public static IX509CertificateChain GenerateCertificateChainWithoutTrust(
            int length,
            string crlServerUri,
            string crlLocalUri,
            bool configureLeafCrl = true,
            Action<TestCertificateGenerator> leafCertificateActionGenerator = null,
            bool revokeEndCertificate = false)
        {
            List<TestCertificate> testCertificates = new();
            X509CertificateChain certificateChain = new();
            Action<TestCertificateGenerator> actionGenerator = CertificateModificationGeneratorForCodeSigningEkuCert;
            Action<TestCertificateGenerator> leafGenerator = leafCertificateActionGenerator ?? actionGenerator;
            X509Certificate2 issuer = null;
            X509Certificate2 certificate = null;
            CertificateRevocationList crl = null;

            for (var i = 0; i < length; i++)
            {
                TestCertificate testCertificate;

                if (i == 0) // root CA cert
                {
                    ChainCertificateRequest chainCertificateRequest = new()
                    {
                        ConfigureCrl = true,
                        CrlLocalBaseUri = crlLocalUri,
                        CrlServerBaseUri = crlServerUri,
                        IsCA = true
                    };

                    testCertificate = TestCertificate.Generate(
                        X509StorePurpose.CodeSigning,
                        actionGenerator,
                        chainCertificateRequest);

                    testCertificates.Add(testCertificate);

                    issuer = certificate = testCertificate.PublicCertWithPrivateKey;
                }
                else if (i < length - 1) // intermediate CA cert
                {
                    ChainCertificateRequest chainCertificateRequest = new ChainCertificateRequest()
                    {
                        ConfigureCrl = true,
                        CrlLocalBaseUri = crlLocalUri,
                        CrlServerBaseUri = crlServerUri,
                        IsCA = true,
                        Issuer = issuer
                    };

                    testCertificate = TestCertificate.Generate(
                        X509StorePurpose.CodeSigning,
                        actionGenerator,
                        chainCertificateRequest);

                    testCertificates.Add(testCertificate);

                    issuer = certificate = testCertificate.PublicCertWithPrivateKey;

                    if (revokeEndCertificate)
                    {
                        crl = testCertificate.Crl;
                    }
                }
                else // leaf cert
                {
                    ChainCertificateRequest chainCertificateRequest = new()
                    {
                        CrlLocalBaseUri = crlLocalUri,
                        CrlServerBaseUri = crlServerUri,
                        IsCA = false,
                        ConfigureCrl = configureLeafCrl,
                        Issuer = issuer
                    };

                    testCertificate = TestCertificate.Generate(
                        X509StorePurpose.CodeSigning,
                        leafGenerator,
                        chainCertificateRequest);

                    certificate = testCertificate.PublicCertWithPrivateKey;

                    if (revokeEndCertificate)
                    {
                        testCertificates[testCertificates.Count - 1].Crl.RevokeCertificate(certificate);
                    }

                    testCertificates.Add(testCertificate);
                }

                certificateChain.Insert(index: 0, certificate);
            }

            foreach (TestCertificate testCertificate in testCertificates)
            {
                testCertificate.Cert.Dispose();
            }

            return certificateChain;
        }

        public static X509CertificateWithKeyInfo GenerateCertificateWithKeyInfo(
            string subjectName,
            Action<TestCertificateGenerator> modifyGenerator,
            global::NuGet.Common.HashAlgorithmName hashAlgorithm = global::NuGet.Common.HashAlgorithmName.SHA256,
            RSASignaturePaddingMode paddingMode = RSASignaturePaddingMode.Pkcs1,
            int publicKeyLength = 2048,
            ChainCertificateRequest chainCertificateRequest = null)
        {
            var rsa = RSA.Create(publicKeyLength);
            var cert = GenerateCertificate(subjectName, modifyGenerator, rsa, hashAlgorithm, paddingMode, chainCertificateRequest);

            return new X509CertificateWithKeyInfo(cert, rsa);
        }

        /// <summary>
        /// Create a self signed certificate with bouncy castle.
        /// </summary>
        public static X509Certificate2 GenerateCertificate(
            string subjectName,
            Action<TestCertificateGenerator> modifyGenerator,
            global::NuGet.Common.HashAlgorithmName hashAlgorithm = global::NuGet.Common.HashAlgorithmName.SHA256,
            RSASignaturePaddingMode paddingMode = RSASignaturePaddingMode.Pkcs1,
            int publicKeyLength = 2048,
            ChainCertificateRequest chainCertificateRequest = null)
        {
            chainCertificateRequest = chainCertificateRequest ?? new ChainCertificateRequest()
            {
                IsCA = true
            };

            // CodeQL [SM03797] This is test code. Some tests use weak keys to test the product's rejection of weak keys. See internal bug 2287165.
            using (var rsa = RSA.Create(publicKeyLength))
            {
                return GenerateCertificate(subjectName, modifyGenerator, rsa, hashAlgorithm, paddingMode, chainCertificateRequest);
            }
        }

        private static X509Certificate2 GenerateCertificate(
            string subjectName,
            Action<TestCertificateGenerator> modifyGenerator,
            RSA rsa,
            global::NuGet.Common.HashAlgorithmName hashAlgorithm,
            RSASignaturePaddingMode paddingMode,
            ChainCertificateRequest chainCertificateRequest)
        {
            if (string.IsNullOrEmpty(subjectName))
            {
                subjectName = "NuGetTest";
            }

            // Create cert
            var subjectDN = $"CN={subjectName}";
            var certGen = new TestCertificateGenerator();

            var isSelfSigned = true;
            X509Certificate2 issuer = null;
            DateTimeOffset? notAfter = null;

            var keyUsage = X509KeyUsageFlags.DigitalSignature;

            if (chainCertificateRequest == null)
            {
                // Self-signed certificates should have this flag set.
                keyUsage |= X509KeyUsageFlags.KeyCertSign;
            }
            else
            {
                if (chainCertificateRequest.Issuer != null)
                {
                    isSelfSigned = false;
                    // for a certificate with an issuer assign Authority Key Identifier
                    issuer = chainCertificateRequest?.Issuer;

                    notAfter = issuer.NotAfter.Subtract(TimeSpan.FromMinutes(5));
                    X509AuthorityKeyIdentifierExtension extension = X509AuthorityKeyIdentifierExtension.CreateFromCertificate(
                        chainCertificateRequest.Issuer,
                        includeKeyIdentifier: true,
                        includeIssuerAndSerial: false);

                    certGen.Extensions.Add(extension);
                }

                if (chainCertificateRequest.ConfigureCrl)
                {
                    // for a certificate in a chain create CRL distribution point extension
                    var issuerDN = chainCertificateRequest?.Issuer?.Subject ?? subjectDN;
                    var crlServerUri = new Uri($"{chainCertificateRequest.CrlServerBaseUri}{issuerDN}.crl");
                    string[] uris = new[] { crlServerUri.AbsoluteUri };
                    X509Extension extension = CertificateRevocationListBuilder.BuildCrlDistributionPointExtension(uris);

                    certGen.Extensions.Add(extension);
                }

                if (chainCertificateRequest.IsCA)
                {
                    // update key usage with CA cert sign and crl sign attributes
                    keyUsage |= X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.KeyCertSign;
                }
            }

            var padding = paddingMode.ToPadding();
            var request = new CertificateRequest(subjectDN, rsa, hashAlgorithm.ConvertToSystemSecurityHashAlgorithmName(), padding);
            bool isCa = isSelfSigned ? true : (chainCertificateRequest?.IsCA ?? false);

            certGen.NotAfter = notAfter ?? DateTime.UtcNow.Add(TimeSpan.FromMinutes(30));
            certGen.NotBefore = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(30));

            byte[] serialNumber = CertificateUtilities.GenerateSerialNumber();

            certGen.SetSerialNumber(serialNumber);

            certGen.Extensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));
            certGen.Extensions.Add(
                new X509KeyUsageExtension(keyUsage, critical: false));
            certGen.Extensions.Add(
                new X509BasicConstraintsExtension(certificateAuthority: isCa, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));

            // Allow changes
            modifyGenerator?.Invoke(certGen);

            foreach (var extension in certGen.Extensions)
            {
                request.CertificateExtensions.Add(extension);
            }

            X509Certificate2 certResult;

            if (isSelfSigned)
            {
                certResult = request.CreateSelfSigned(certGen.NotBefore, certGen.NotAfter);
            }
            else
            {
                using (var temp = request.Create(issuer, certGen.NotBefore, certGen.NotAfter, certGen.SerialNumber))
                {
                    certResult = temp.CopyWithPrivateKey(rsa);
                }
            }

#if NET9_0_OR_GREATER
            return X509CertificateLoader.LoadPkcs12(certResult.Export(X509ContentType.Pkcs12), password: (string)null, keyStorageFlags: X509KeyStorageFlags.Exportable);
#else
            return new X509Certificate2(certResult.Export(X509ContentType.Pkcs12), password: (string)null, keyStorageFlags: X509KeyStorageFlags.Exportable);
#endif
        }

        private static RSASignaturePadding ToPadding(this RSASignaturePaddingMode mode)
        {
            switch (mode)
            {
                case RSASignaturePaddingMode.Pkcs1: return RSASignaturePadding.Pkcs1;
                case RSASignaturePaddingMode.Pss: return RSASignaturePadding.Pss;
            }

            return null;
        }

        /// <summary>
        /// Create a self signed certificate.
        /// </summary>
        public static X509Certificate2 GenerateCertificate(string subjectName, RSA key)
        {
            if (string.IsNullOrEmpty(subjectName))
            {
                subjectName = "NuGetTest";
            }

            var subjectDN = new X500DistinguishedName($"CN={subjectName}");
            var hashAlgorithm = System.Security.Cryptography.HashAlgorithmName.SHA256;
            var request = new CertificateRequest(subjectDN, key, hashAlgorithm, RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, critical: true));
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(new OidCollection { new Oid(Oids.CodeSigningEku) }, critical: true));

            var certResult = request.CreateSelfSigned(notBefore: DateTime.UtcNow.Subtract(TimeSpan.FromHours(1)), notAfter: DateTime.UtcNow.Add(TimeSpan.FromHours(1)));

#if NET9_0_OR_GREATER
            return X509CertificateLoader.LoadPkcs12(certResult.Export(X509ContentType.Pkcs12), password: (string)null, keyStorageFlags: X509KeyStorageFlags.Exportable);
#else
            return new X509Certificate2(certResult.Export(X509ContentType.Pkcs12), password: (string)null, keyStorageFlags: X509KeyStorageFlags.Exportable);
#endif
        }

        public static X509Certificate2 GenerateCertificate(
            string issuerName,
            string subjectName,
            RSA issuerAlgorithm,
            RSA algorithm)
        {
            var subjectDN = $"CN={subjectName}";
            var issuerDN = new X500DistinguishedName($"CN={issuerName}");

            var notAfter = DateTime.UtcNow.Add(TimeSpan.FromHours(1));
            var notBefore = DateTime.UtcNow.Subtract(TimeSpan.FromHours(1));

            var random = new Random();
            var serial = random.Next();
            var serialNumber = BitConverter.GetBytes(serial);
            Array.Reverse(serialNumber);

            var hashAlgorithm = System.Security.Cryptography.HashAlgorithmName.SHA256;
            var request = new CertificateRequest(subjectDN, algorithm, hashAlgorithm, RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, critical: true));
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(new OidCollection { new Oid(Oids.CodeSigningEku) }, critical: true));

            var generator = X509SignatureGenerator.CreateForRSA(issuerAlgorithm, RSASignaturePadding.Pkcs1);

            using (var temp = request.Create(issuerDN, generator, notBefore, notAfter, serialNumber))
            {
                var certResult = temp.CopyWithPrivateKey(algorithm);
#if NET9_0_OR_GREATER
                return X509CertificateLoader.LoadPkcs12(certResult.Export(X509ContentType.Pkcs12), password: (string)null, keyStorageFlags: X509KeyStorageFlags.Exportable);
#else
                return new X509Certificate2(certResult.Export(X509ContentType.Pkcs12), password: (string)null, keyStorageFlags: X509KeyStorageFlags.Exportable);
#endif
            }
        }

        public static X509Certificate2 GenerateSelfIssuedCertificate(bool isCa)
        {
            using (var rsa = RSA.Create(keySizeInBits: 2048))
            {
                var subjectName = new X500DistinguishedName($"C=US,S=WA,L=Redmond,O=NuGet,CN=NuGet Test Self-Issued Certificate ({Guid.NewGuid().ToString()})");
                var hashAlgorithm = System.Security.Cryptography.HashAlgorithmName.SHA256;
                var request = new CertificateRequest(subjectName, rsa, hashAlgorithm, RSASignaturePadding.Pkcs1);

                var keyUsages = X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign;

                if (isCa)
                {
                    keyUsages |= X509KeyUsageFlags.KeyCertSign;
                }

                var skiExtension = new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false);

                request.CertificateExtensions.Add(skiExtension);

                ReadOnlySpan<byte> skidBytes = HexConverter.ToByteArray(skiExtension.SubjectKeyIdentifier);
                X509AuthorityKeyIdentifierExtension akiExtension = X509AuthorityKeyIdentifierExtension.CreateFromSubjectKeyIdentifier(skidBytes);

                request.CertificateExtensions.Add(akiExtension);
                request.CertificateExtensions.Add(
                    new X509BasicConstraintsExtension(certificateAuthority: isCa, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(keyUsages, critical: true));

                var now = DateTime.UtcNow;
                var certResult = request.CreateSelfSigned(notBefore: now, notAfter: now.AddHours(1));

#if NET9_0_OR_GREATER
                return X509CertificateLoader.LoadPkcs12(certResult.Export(X509ContentType.Pkcs12), password: (string)null, keyStorageFlags: X509KeyStorageFlags.Exportable);
#else
                return new X509Certificate2(certResult.Export(X509ContentType.Pkcs12), password: (string)null, keyStorageFlags: X509KeyStorageFlags.Exportable);
#endif
            }
        }

        public static RSA GenerateKeyPair(int publicKeyLength)
        {
            return RSA.Create(publicKeyLength);
        }

        /// <summary>
        /// Generates a SignedCMS object for some content.
        /// </summary>
        /// <param name="content"></param>
        /// <param name="cert">Certificate for cms signer</param>
        /// <returns>SignedCms object</returns>
        public static SignedCms GenerateSignedCms(X509Certificate2 cert, byte[] content)
        {
            var contentInfo = new ContentInfo(content);
            var cmsSigner = new CmsSigner(SubjectIdentifierType.SubjectKeyIdentifier, cert);
            var signingTime = new Pkcs9SigningTime();

            cmsSigner.SignedAttributes.Add(
                new CryptographicAttributeObject(
                    signingTime.Oid,
                    new AsnEncodedDataCollection(signingTime)));

            var cms = new SignedCms(contentInfo);
            cms.ComputeSignature(cmsSigner);

            return cms;
        }

        /// <summary>
        /// Generates a SignedCMS object for some content.
        /// </summary>
        /// <param name="content"></param>
        /// <param name="cert">Certificate for cms signer</param>
        /// <returns>SignedCms object</returns>
        public static SignedCms GenerateRepositoryCountersignedSignedCms(X509Certificate2 cert, byte[] content)
        {
            var contentInfo = new ContentInfo(content);
            var hashAlgorithm = global::NuGet.Common.HashAlgorithmName.SHA256;

            using (var primarySignatureRequest = new AuthorSignPackageRequest(new X509Certificate2(cert), hashAlgorithm))
            using (var countersignatureRequest = new RepositorySignPackageRequest(new X509Certificate2(cert), hashAlgorithm, hashAlgorithm, new Uri("https://api.nuget.org/v3/index.json"), null))
            {
                var cmsSigner = SigningUtility.CreateCmsSigner(primarySignatureRequest, NullLogger.Instance);

                var cms = new SignedCms(contentInfo);
                cms.ComputeSignature(cmsSigner);

                var counterCmsSigner = SigningUtility.CreateCmsSigner(countersignatureRequest, NullLogger.Instance);
                cms.SignerInfos[0].ComputeCounterSignature(counterCmsSigner);

                return cms;
            }
        }

        /// <summary>
        /// Returns the public cert without the private key.
        /// </summary>
        public static X509Certificate2 GetPublicCert(X509Certificate2 cert)
        {
#if NET9_0_OR_GREATER
            return X509CertificateLoader.LoadCertificate(cert.Export(X509ContentType.Cert));
#else
            return new X509Certificate2(cert.Export(X509ContentType.Cert));
#endif
        }

        /// <summary>
        /// Returns the public cert with the private key.
        /// </summary>
        public static X509Certificate2 GetPublicCertWithPrivateKey(X509Certificate2 cert)
        {
            var password = new Guid().ToString();
#if NET9_0_OR_GREATER
            return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx, password), password, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
#else
            return new X509Certificate2(cert.Export(X509ContentType.Pfx, password), password, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
#endif
        }

        public static TrustedTestCert<TestCertificate> GenerateTrustedTestCertificate()
        {
            var actionGenerator = CertificateModificationGeneratorForCodeSigningEkuCert;

            // Code Sign EKU needs trust to a root authority
            // Add the cert to Root CA list in LocalMachine as it does not prompt a dialog
            // This makes all the associated tests to require admin privilege
            return TestCertificate.Generate(X509StorePurpose.CodeSigning, actionGenerator).WithTrust();
        }

        public static TrustedTestCert<TestCertificate> GenerateTrustedTestCertificateExpired()
        {
            var actionGenerator = CertificateModificationGeneratorExpiredCert;

            // Code Sign EKU needs trust to a root authority
            // Add the cert to Root CA list in LocalMachine as it does not prompt a dialog
            // This makes all the associated tests to require admin privilege
            return TestCertificate.Generate(X509StorePurpose.CodeSigning, actionGenerator).WithTrust();
        }

        public static TrustedTestCert<TestCertificate> GenerateTrustedTestCertificateNotYetValid()
        {
            var actionGenerator = CertificateModificationGeneratorNotYetValidCert;

            // Code Sign EKU needs trust to a root authority
            // Add the cert to Root CA list in LocalMachine as it does not prompt a dialog
            // This makes all the associated tests to require admin privilege
            return TestCertificate.Generate(X509StorePurpose.CodeSigning, actionGenerator).WithTrust();
        }

        public static TrustedTestCert<TestCertificate> GenerateTrustedTestCertificateThatWillExpireSoon(TimeSpan expiresIn)
        {
            var actionGenerator = CertificateModificationGeneratorForCertificateThatWillExpireSoon(expiresIn);

            // Code Sign EKU needs trust to a root authority
            // Add the cert to Root CA list in LocalMachine as it does not prompt a dialog
            // This makes all the associated tests to require admin privilege
            return TestCertificate.Generate(X509StorePurpose.CodeSigning, actionGenerator).WithTrust();
        }

        public static bool AreVerifierSettingsEqual(SignedPackageVerifierSettings first, SignedPackageVerifierSettings second)
        {
            return first.AllowIgnoreTimestamp == second.AllowIgnoreTimestamp &&
                first.AllowIllegal == second.AllowIllegal &&
                first.AllowMultipleTimestamps == second.AllowMultipleTimestamps &&
                first.AllowNoTimestamp == second.AllowNoTimestamp &&
                first.AllowUnknownRevocation == second.AllowUnknownRevocation &&
                first.ReportUnknownRevocation == second.ReportUnknownRevocation &&
                first.AllowUnsigned == second.AllowUnsigned &&
                first.AllowUntrusted == second.AllowUntrusted &&
                first.VerificationTarget == second.VerificationTarget &&
                first.SignaturePlacement == second.SignaturePlacement &&
                first.RepositoryCountersignatureVerificationBehavior == second.RepositoryCountersignatureVerificationBehavior;
        }

        public static DisposableList<IDisposable> RegisterDefaultResponders(
            this ISigningTestServer testServer,
            TimestampService timestampService)
        {
            var responders = new DisposableList<IDisposable>();
            var ca = timestampService.CertificateAuthority;

            while (ca != null)
            {
                responders.Add(testServer.RegisterResponder(ca));
                responders.Add(testServer.RegisterResponder(ca.OcspResponder));

                ca = ca.Parent;
            }

            responders.Add(testServer.RegisterResponder(timestampService));

            return responders;
        }

        public static async Task<VerifySignaturesResult> VerifySignatureAsync(SignedPackageArchive signPackage, SignedPackageVerifierSettings settings)
        {
            var verificationProviders = new[] { new SignatureTrustAndValidityVerificationProvider() };
            var verifier = new PackageSignatureVerifier(verificationProviders);
            var result = await verifier.VerifySignaturesAsync(signPackage, settings, CancellationToken.None);
            return result;
        }

        public static byte[] GetResourceBytes(string name)
        {
            return ResourceTestUtility.GetResourceBytes($"Microsoft.Internal.NuGet.Testing.SignedPackages.compiler.resources.{name}", typeof(SigningTestUtility));
        }

        public static X509Certificate2 GetCertificate(string name)
        {
            var bytes = GetResourceBytes(name);

#if NET9_0_OR_GREATER
            return X509CertificateLoader.LoadCertificate(bytes);
#else
            return new X509Certificate2(bytes);
#endif
        }

        public static byte[] GetHash(X509Certificate2 certificate, global::NuGet.Common.HashAlgorithmName hashAlgorithm)
        {
            return hashAlgorithm.ComputeHash(certificate.RawData);
        }

        public static void VerifySerialNumber(X509Certificate2 certificate, global::NuGet.Packaging.Signing.IssuerSerial issuerSerial)
        {
            ReadOnlySpan<byte> serialNumber = certificate.GetSerialNumberBigEndian();

            VerifyByteArrays(serialNumber.ToArray(), issuerSerial.SerialNumber);
        }

        public static void VerifySerialNumber(BigInteger serialNumber1, byte[] serialNumber2)
        {
            byte[] expected = serialNumber1.ToByteArray();

            Array.Reverse(expected);

            VerifyByteArrays(expected, serialNumber2);
        }

        public static void VerifyByteSequences(ReadOnlyMemory<byte> expected, ReadOnlyMemory<byte> actual)
        {
            Assert.Equal(expected.Length, actual.Length);

            VerifyByteArrays(expected.Span.ToArray(), actual.Span.ToArray());
        }

        public static void VerifyByteArrays(byte[] expected, byte[] actual)
        {
            var expectedHex = BitConverter.ToString(expected).Replace("-", "");
            var actualHex = BitConverter.ToString(actual).Replace("-", "");

            Assert.Equal(expectedHex, actualHex);
        }

        //We will not change the original X509ChainStatus.StatusInformation of OfflineRevocation if we directly call API CertificateChainUtility.GetCertificateChain (or SigningUtility.Verify)
        //So if we use APIs above to verify the results of chain.build, we should use AssertOfflineRevocation
        public static void AssertOfflineRevocation(IEnumerable<ILogMessage> issues, LogLevel logLevel)
        {
            string offlineRevocation = X509ChainStatusFlags.OfflineRevocation.ToString();

            bool isOfflineRevocation = issues.Any(issue =>
                issue.Code == NuGetLogCode.NU3018 &&
                issue.Level == logLevel &&
                issue.Message.Split(new[] { ' ', ':' }).Any(WORDEXTFLAGS => WORDEXTFLAGS == offlineRevocation));

            Assert.True(isOfflineRevocation);
        }

        //We will change the original X509ChainStatus.StatusInformation of OfflineRevocation to VerifyCertTrustOfflineWhileRevocationModeOffline or VerifyCertTrustOfflineWhileRevocationModeOnline in Signature.cs and Timestamp.cs
        //So if we use APIs above to verify the results of chain.build, we should use assert AssertOfflineRevocationOnlineMode and AssertOfflineRevocationOfflineMode
        public static void AssertOfflineRevocationOnlineMode(IEnumerable<SignatureLog> issues, LogLevel logLevel)
        {
            AssertOfflineRevocationOnlineMode(issues, logLevel, NuGetLogCode.NU3018);
        }

        public static void AssertOfflineRevocationOnlineMode(IEnumerable<SignatureLog> issues, LogLevel logLevel, NuGetLogCode code)
        {
            Assert.Contains(issues, issue =>
                issue.Code == code &&
                issue.Level == logLevel &&
                issue.Message.Contains("The revocation function was unable to check revocation because the revocation server could not be reached. For more information, visit https://aka.ms/certificateRevocationMode."));
        }

        public static void AssertOfflineRevocationOfflineMode(IEnumerable<SignatureLog> issues)
        {
            AssertOfflineRevocationOfflineMode(issues, LogLevel.Information, NuGetLogCode.Undefined);
        }

        public static void AssertOfflineRevocationOfflineMode(IEnumerable<SignatureLog> issues, LogLevel logLevel, NuGetLogCode code)
        {
            Assert.Contains(issues, issue =>
                issue.Code == code &&
                issue.Level == logLevel &&
                issue.Message.Contains("The revocation function was unable to check revocation because the certificate is not available in the cached certificate revocation list and NUGET_CERT_REVOCATION_MODE environment variable has been set to offline. For more information, visit https://aka.ms/certificateRevocationMode."));
        }

        public static void AssertRevocationStatusUnknown(IEnumerable<ILogMessage> issues, LogLevel logLevel)
        {
            AssertRevocationStatusUnknown(issues, logLevel, NuGetLogCode.NU3018);
        }

        public static void AssertRevocationStatusUnknown(IEnumerable<ILogMessage> issues, LogLevel logLevel, NuGetLogCode code)
        {
            string revocationStatusUnknown = X509ChainStatusFlags.RevocationStatusUnknown.ToString();

            bool isRevocationStatusUnknown = issues.Any(issue =>
                issue.Code == code &&
                issue.Level == logLevel &&
                issue.Message.Split(new[] { ' ', ':' }).Any(WORDEXTFLAGS => WORDEXTFLAGS == revocationStatusUnknown));

            Assert.True(isRevocationStatusUnknown);
        }

        public static void AssertUntrustedRoot(IEnumerable<ILogMessage> issues, NuGetLogCode code, LogLevel logLevel)
        {
            string untrustedRoot = X509ChainStatusFlags.UntrustedRoot.ToString();

            bool isUntrustedRoot = issues.Any(issue =>
                issue.Code == code &&
                issue.Level == logLevel &&
                (issue.Message.Contains("certificate is not trusted by the trust provider") ||
                 issue.Message.Split(new[] { ' ', ':' }).Any(WORDEXTFLAGS => WORDEXTFLAGS == untrustedRoot)));

            Assert.True(isUntrustedRoot);

#if NET5_0_OR_GREATER
            if (!RuntimeEnvironmentHelper.IsWindows)
            {
                bool hasNU3042 = issues.Any(issue => issue.Code == NuGetLogCode.NU3042);

                Assert.True(hasNU3042);
            }
#endif
        }

        public static void AssertUntrustedRoot(IEnumerable<ILogMessage> issues, LogLevel logLevel)
        {
            AssertUntrustedRoot(issues, NuGetLogCode.NU3018, logLevel);
        }

        public static void AssertNotTimeValid(IEnumerable<ILogMessage> issues, LogLevel logLevel)
        {
            string notTimeValid = X509ChainStatusFlags.NotTimeValid.ToString();

            bool isNotTimeValid = issues.Any(issue =>
                issue.Code == NuGetLogCode.NU3018 &&
                issue.Level == logLevel &&
                issue.Message.Split(new[] { ' ', ':' }).Any(WORDEXTFLAGS => WORDEXTFLAGS == notTimeValid));

            Assert.True(isNotTimeValid);
        }

        public static string AddSignatureLogPrefix(string log, PackageIdentity package, string source)
        {
            return $"{string.Format(CultureInfo.CurrentCulture, SignatureLogPrefix, package.Id, package.Version, source)} {log}";
        }
    }
}
