// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages
{
    /// <summary>
    /// Test certificate pair.
    /// </summary>
    public sealed class TestCertificate
    {
        private readonly X509StorePurpose _storePurpose;

        /// <summary>
        /// Cert
        /// </summary>
        public X509Certificate2 Cert { get; set; }

        /// <summary>
        /// Public cert.
        /// </summary>
        public X509Certificate2 PublicCert => SigningTestUtility.GetPublicCert(Cert);

        /// <summary>
        /// Public cert.
        /// </summary>
        public X509Certificate2 PublicCertWithPrivateKey => SigningTestUtility.GetPublicCertWithPrivateKey(Cert);

        /// <summary>
        /// Certificate Revocation List associated with a certificate.
        /// This will be null if the certificate was not created as a CA certificate.
        /// </summary>
        public CertificateRevocationList Crl { get; set; }

        public TestCertificate(X509StorePurpose storePurpose)
        {
            _storePurpose = storePurpose;
        }

        /// <summary>
        /// Trust the PublicCert cert for the life of the object.
        /// </summary>
        /// <remarks>Dispose of the object returned!</remarks>
        /// According to https://github.com/dotnet/corefx/blob/master/Documentation/architecture/cross-platform-cryptography.md#x509store
        /// Linux could not read/write LocalMachine\Root , but could only read/write CurrentUser\Root
        public TrustedTestCert<TestCertificate> WithTrust()
        {
            StoreLocation storeLocation = CertificateStoreUtilities.GetTrustedCertificateStoreLocation();

            return new TrustedTestCert<TestCertificate>(
                this,
                e => PublicCert,
                _storePurpose,
                StoreName.Root,
                storeLocation);
        }

        /// <summary>
        /// Trust the PublicCert cert for the life of the object.
        /// </summary>
        /// <remarks>Dispose of the object returned!</remarks>
        public TrustedTestCert<TestCertificate> WithPrivateKeyAndTrust(StoreName storeName = StoreName.TrustedPeople)
        {
            StoreLocation storeLocation = CertificateStoreUtilities.GetTrustedCertificateStoreLocation();

            return new TrustedTestCert<TestCertificate>(
                this,
                e => PublicCertWithPrivateKey,
                _storePurpose,
                storeName,
                storeLocation);
        }

        /// <summary>
        /// Trust the PublicCert cert as intermediate CA certificate.
        /// </summary>
        /// <remarks>Dispose of the object returned!</remarks>
        /// On MacOs, there is no StoreName.CertificateAuthority, so add to LocalMachine\My instead.
        internal TrustedTestCert<TestCertificate> WithPrivateKeyAndTrustForIntermediateCertificateAuthority()
        {
            StoreName storeName = CertificateStoreUtilities.GetCertificateAuthorityStoreName();
            StoreLocation storeLocation = CertificateStoreUtilities.GetTrustedCertificateStoreLocation();

            return new TrustedTestCert<TestCertificate>(
                this,
                e => PublicCertWithPrivateKey,
                _storePurpose,
                storeName,
                storeLocation);
        }

        /// <summary>
        /// Trust the PublicCert cert as leaf or self-issued.
        /// </summary>
        /// <remarks>Dispose of the object returned!</remarks>
        /// On MacOs, if we add the leaf or self-issued certificate into LocalMachine\Root, the private key will not be accessed. So the dotnet signing command tests will fail for:
        ///  "Object contains only the public half of a key pair. A private key must also be provided."
        internal TrustedTestCert<TestCertificate> WithPrivateKeyAndTrustForLeafOrSelfIssued()
        {
            StoreName storeName = CertificateStoreUtilities.GetTrustedCertificateStoreNameForLeafOrSelfIssuedCertificate();
            StoreLocation storeLocation = CertificateStoreUtilities.GetTrustedCertificateStoreLocationForLeafOrSelfIssuedCertificate();

            return new TrustedTestCert<TestCertificate>(
                this,
                e => PublicCertWithPrivateKey,
                _storePurpose,
                storeName,
                storeLocation);
        }

        public static string GenerateCertificateName()
        {
            return "NuGetTest-" + Guid.NewGuid().ToString();
        }

        public static TestCertificate Generate(
            X509StorePurpose storePurpose,
            Action<TestCertificateGenerator> modifyGenerator = null,
            ChainCertificateRequest chainCertificateRequest = null)
        {
            string certName = GenerateCertificateName();
            X509CertificateWithKeyInfo cert = SigningTestUtility.GenerateCertificateWithKeyInfo(
                certName,
                modifyGenerator,
                chainCertificateRequest: chainCertificateRequest);
            CertificateRevocationList crl = null;

            // create a crl only if the certificate is part of a chain and it is a CA and ConfigureCrl is true
            if (chainCertificateRequest != null && chainCertificateRequest.IsCA && chainCertificateRequest.ConfigureCrl)
            {
                crl = new CertificateRevocationList(cert, chainCertificateRequest.CrlLocalBaseUri);
            }

            var testCertificate = new TestCertificate(storePurpose)
            {
                Cert = cert.Certificate,
                Crl = crl
            };

            return testCertificate;
        }
    }
}
