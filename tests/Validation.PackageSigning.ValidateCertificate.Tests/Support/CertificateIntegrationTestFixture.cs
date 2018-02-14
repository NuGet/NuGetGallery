// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;
using Test.Utility.Signing;
using Validation.PackageSigning.Core.Tests.Support;
using BCCertificate = Org.BouncyCastle.X509.X509Certificate;

namespace Validation.PackageSigning.ValidateCertificate.Tests.Support
{
    using CoreCertificateIntegrationTestFixture = Core.Tests.Support.CertificateIntegrationTestFixture;

    public class CertificateIntegrationTestFixture : CoreCertificateIntegrationTestFixture
    {
        public async Task<X509Certificate2> GetIntermediateCaCertificate()
        {
            var certificate = new X509Certificate2();
            var encodedCert = (await GetCertificateAuthority()).Certificate.GetEncoded();

            certificate.Import(encodedCert);

            return certificate;
        }

        public async Task RevokeCertificateAuthority()
        {
            var ca = await GetCertificateAuthority();
            var rootCa = await GetRootCertificateAuthority();

            rootCa.Revoke(ca.Certificate, reason: 0, revocationDate: DateTimeOffset.UtcNow);
        }

        public async Task<X509Certificate2> GetTimestampingCertificateAsync()
        {
            var ca = await GetCertificateAuthority();
            return CreateTimestampingCertificate(ca);
        }

        public X509Certificate2 CreateTimestampingCertificate(CertificateAuthority ca)
        {
            void CustomizeAsTimestampingCertificate(X509V3CertificateGenerator generator)
            {
                generator.AddTimestampingEku();
                generator.AddAuthorityInfoAccess(ca, addOcsp: true, addCAIssuers: true);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca, "Timestamping", CustomizeAsTimestampingCertificate);

            return certificate;
        }

        public async Task<X509Certificate2> GetUnknownSigningCertificateAsync()
        {
            var ca = await GetCertificateAuthority();

            void CustomizeAsUnknownSigningCertificate(X509V3CertificateGenerator generator)
            {
                generator.AddSigningEku();
                generator.AddAuthorityInfoAccess(ca, addOcsp: false, addCAIssuers: true);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca, "Unknown Signing", CustomizeAsUnknownSigningCertificate);

            return certificate;
        }

        public async Task<X509Certificate2> GetRevokedSigningCertificateAsync(DateTimeOffset revocationDate)
        {
            var ca = await GetCertificateAuthority();

            void CustomizeAsSigningCertificate(X509V3CertificateGenerator generator)
            {
                generator.AddSigningEku();
                generator.AddAuthorityInfoAccess(ca, addOcsp: true, addCAIssuers: true);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca, "Revoked Signing", CustomizeAsSigningCertificate);

            ca.Revoke(publicCertificate, reason: 0, revocationDate: revocationDate);

            return certificate;
        }

        public async Task<X509Certificate2> GetRevokedParentSigningCertificateAsync()
        {
            var testServer = await GetTestServerAsync();
            var rootCa = await GetRootCertificateAuthority();
            var intermediateCa = rootCa.CreateIntermediateCertificateAuthority();

            var responders = GetResponders();

            responders.AddRange(testServer.RegisterResponders(intermediateCa));

            rootCa.Revoke(intermediateCa.Certificate, reason: 0, revocationDate: DateTimeOffset.UtcNow);

            return CreateSigningCertificate(intermediateCa);
        }

        public async Task<X509Certificate2> GetPartialChainSigningCertificateAsync()
        {
            var ca = await GetCertificateAuthority();

            void CustomizeAsPartialChainSigningCertificate(X509V3CertificateGenerator generator)
            {
                generator.AddSigningEku();
                generator.AddAuthorityInfoAccess(ca, addOcsp: true, addCAIssuers: false);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca, "Untrusted Signing", CustomizeAsPartialChainSigningCertificate);

            return certificate;
        }

        public async Task<X509Certificate2> GetPartialChainAndRevokedSigningCertificateAsync()
        {
            var ca = await GetCertificateAuthority();

            void CustomizeAsPartialChainAndRevokedCertificate(X509V3CertificateGenerator generator)
            {
                generator.AddSigningEku();
                generator.AddAuthorityInfoAccess(ca, addOcsp: true, addCAIssuers: false);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca, "Untrusted and Revoked Signing", CustomizeAsPartialChainAndRevokedCertificate);

            ca.Revoke(publicCertificate, reason: 0, revocationDate: DateTimeOffset.UtcNow);

            return certificate;
        }

        public async Task<X509Certificate2> GetExpiredSigningCertificateAsync()
        {
            var ca = await GetCertificateAuthority();

            void CustomizeAsExpiredCertificate(X509V3CertificateGenerator generator)
            {
                generator.MakeExpired();
                generator.AddAuthorityInfoAccess(ca, addOcsp: true, addCAIssuers: true);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca, "Expired Signing", CustomizeAsExpiredCertificate);

            return certificate;
        }

        public async Task<X509Certificate2> GetExpiredAndRevokedSigningCertificateAsync()
        {
            var ca = await GetCertificateAuthority();

            void CustomizeAsExpiredAndRevokedCertificate(X509V3CertificateGenerator generator)
            {
                generator.MakeExpired();
                generator.AddAuthorityInfoAccess(ca, addOcsp: true, addCAIssuers: true);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca, "Expired Signing", CustomizeAsExpiredAndRevokedCertificate);

            ca.Revoke(publicCertificate, reason: 0, revocationDate: DateTimeOffset.UtcNow);

            return certificate;
        }

        public async Task<X509Certificate2> GetWeakSignatureParentSigningCertificateAsync()
        {
            var testServer = await GetTestServerAsync();
            var rootCa = await GetRootCertificateAuthority();

            var keyPair = SigningTestUtility.GenerateKeyPair(publicKeyLength: 512);
            var certificate = IssueCaCertificate(rootCa, keyPair.Public);
            var responders = GetResponders();

            var intermediateCa = NewCertificateAuthority(certificate, keyPair, rootCa.SharedUri, parentCa: rootCa);

            responders.AddRange(testServer.RegisterResponders(intermediateCa));

            return CreateSigningCertificate(intermediateCa);
        }

        private BCCertificate IssueCaCertificate(
            CertificateAuthority ca,
            AsymmetricKeyParameter publicKey,
            Action<X509V3CertificateGenerator> customizeCertificate = null)
        {
            var method = typeof(CertificateAuthority).GetMethod("IssueCaCertificate", BindingFlags.Instance | BindingFlags.NonPublic);

            if (method == null)
            {
                throw new Exception($"Could not find method {nameof(CertificateAuthority)}.IssueCaCertificate");
            }

            var parameters = method.GetParameters();

            if (parameters.Length != 2 ||
                parameters[0].ParameterType != typeof(AsymmetricKeyParameter) ||
                parameters[1].ParameterType != typeof(Action<X509V3CertificateGenerator>) ||
                method.ReturnType != typeof(BCCertificate))
            {
                throw new Exception($"{nameof(CertificateAuthority)}'s IssueCaCertificate parameters or return type have changed");
            }

            return (BCCertificate)method.Invoke(ca, new object[] { publicKey, customizeCertificate });
        }

        private CertificateAuthority NewCertificateAuthority(
            BCCertificate certificate,
            AsymmetricCipherKeyPair keyPair,
            Uri sharedUri,
            CertificateAuthority parentCa)
        {
            var constructors = typeof(CertificateAuthority).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance);

            if (constructors.Length != 1)
            {
                throw new Exception($"Constructors for {nameof(CertificateAuthority)} have changed");
            }

            var parameters = constructors[0].GetParameters();

            if (parameters.Length != 4 ||
                parameters[0].ParameterType != typeof(BCCertificate) ||
                parameters[1].ParameterType != typeof(AsymmetricCipherKeyPair) ||
                parameters[2].ParameterType != typeof(Uri)  ||
                parameters[3].ParameterType != typeof(CertificateAuthority))
            {
                throw new Exception($"{nameof(CertificateAuthority)}'s constructor parameters have changed");
            }

            return (CertificateAuthority)constructors[0].Invoke(new object[] { certificate, keyPair, sharedUri, parentCa });
        }
    }
}
