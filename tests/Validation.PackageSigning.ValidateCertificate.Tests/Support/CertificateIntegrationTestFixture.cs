﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Internal.NuGet.Testing.SignedPackages;
using Validation.PackageSigning.Core;
using Validation.PackageSigning.Core.Tests.Support;

namespace Validation.PackageSigning.ValidateCertificate.Tests.Support
{
    using CoreCertificateIntegrationTestFixture = Core.Tests.Support.CertificateIntegrationTestFixture;

    public class CertificateIntegrationTestFixture : CoreCertificateIntegrationTestFixture
    {
        public async Task RevokeCertificateAuthority()
        {
            var ca = await GetCertificateAuthorityAsync();
            var rootCa = await GetRootCertificateAuthorityAsync();

            rootCa.Revoke(
                ca.Certificate,
                reason: X509RevocationReason.Unspecified,
                revocationDate: DateTimeOffset.UtcNow);
        }

        public async Task<X509Certificate2> GetTimestampingCertificateAsync()
        {
            var ca = await GetCertificateAuthorityAsync();
            return CreateTimestampingCertificate(ca);
        }

        public X509Certificate2 CreateTimestampingCertificate(CertificateAuthority ca)
        {
            void CustomizeAsTimestampingCertificate(CertificateRequest certificateRequest)
            {
                certificateRequest.AddTimestampingEku();
                certificateRequest.AddAuthorityInfoAccess(ca, addOcsp: true, addCAIssuers: true);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca, "Timestamping", CustomizeAsTimestampingCertificate);

            return certificate;
        }

        public async Task<X509Certificate2> GetUnknownSigningCertificateAsync()
        {
            var ca = await GetCertificateAuthorityAsync();

            void CustomizeAsUnknownSigningCertificate(CertificateRequest certificateRequest)
            {
                certificateRequest.AddSigningEku();
                certificateRequest.AddAuthorityInfoAccess(ca, addOcsp: false, addCAIssuers: true);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca, "Unknown Signing", CustomizeAsUnknownSigningCertificate);

            return certificate;
        }

        public async Task<X509Certificate2> GetRevokedSigningCertificateAsync(DateTimeOffset revocationDate)
        {
            var ca = await GetCertificateAuthorityAsync();

            void CustomizeAsSigningCertificate(CertificateRequest certificateRequest)
            {
                certificateRequest.AddSigningEku();
                certificateRequest.AddAuthorityInfoAccess(ca, addOcsp: true, addCAIssuers: true);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca, "Revoked Signing", CustomizeAsSigningCertificate);

            ca.Revoke(publicCertificate, reason: X509RevocationReason.Unspecified, revocationDate: revocationDate);

            return certificate;
        }

        public async Task<CertificateWithCustomIntermediatesResult> GetRevokedSigningCertificateAsync(DateTimeOffset revocationDate, DateTimeOffset crlUpdateTime)
        {
            var testServer = await GetTestServerAsync();

            var ca = await GetCertificateAuthorityAsync();
            var ca2 = ca.CreateIntermediateCertificateAuthority();
            var responders = new DisposableList<IDisposable>();

            var ca2Responder = OcspResponder.Create(ca2, new OcspResponderOptions
            {
                ThisUpdate = crlUpdateTime,
            });

            responders.Add(testServer.RegisterResponder(ca2));
            responders.Add(testServer.RegisterResponder(ca2Responder));

            void CustomizeAsSigningCertificate(CertificateRequest certificateRequest)
            {
                certificateRequest.AddSigningEku();
                certificateRequest.AddAuthorityInfoAccess(ca2, addOcsp: true, addCAIssuers: true);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca2, "Revoked Signing", CustomizeAsSigningCertificate);

            var caCert = new X509Certificate2(ca.Certificate);
            var ca2Cert = new X509Certificate2(ca2.Certificate);

            ca2.Revoke(publicCertificate, reason: X509RevocationReason.Unspecified, revocationDate: revocationDate);

            return new CertificateWithCustomIntermediatesResult(
                        certificate,
                        new[] { caCert, ca2Cert },
                        responders);
        }

        public async Task<X509Certificate2> GetRevokedTimestampingCertificateAsync(DateTimeOffset revocationDate)
        {
            var ca = await GetCertificateAuthorityAsync();

            void CustomizeAsSigningCertificate(CertificateRequest certificateRequest)
            {
                certificateRequest.AddTimestampingEku();
                certificateRequest.AddAuthorityInfoAccess(ca, addOcsp: true, addCAIssuers: true);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca, "Revoked Timestamping", CustomizeAsSigningCertificate);

            ca.Revoke(publicCertificate, reason: X509RevocationReason.Unspecified, revocationDate: revocationDate);

            return certificate;
        }

        public async Task<X509Certificate2> GetRevokedParentSigningCertificateAsync()
        {
            var testServer = await GetTestServerAsync();
            var rootCa = await GetRootCertificateAuthorityAsync();
            var intermediateCa = rootCa.CreateIntermediateCertificateAuthority();

            var responders = GetResponders();

            responders.AddRange(testServer.RegisterResponders(intermediateCa));

            rootCa.Revoke(intermediateCa.Certificate, reason: X509RevocationReason.Unspecified, revocationDate: DateTimeOffset.UtcNow);

            return CreateSigningCertificate(intermediateCa);
        }

        public async Task<CertificateWithCustomIntermediatesResult> GetPartialChainSigningCertificateAsync()
        {
            var testServer = await GetTestServerAsync();

            var ca = await GetCertificateAuthorityAsync();
            var ca2 = ca.CreateIntermediateCertificateAuthority();
            var responders = new DisposableList<IDisposable>();

            responders.Add(testServer.RegisterResponder(ca2.OcspResponder));

            void CustomizeAsPartialChainSigningCertificate(CertificateRequest certificateRequest)
            {
                certificateRequest.AddSigningEku();
                certificateRequest.AddAuthorityInfoAccess(ca2, addOcsp: true, addCAIssuers: true);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca2, "Untrusted Signing", CustomizeAsPartialChainSigningCertificate);

            var caCert = new X509Certificate2(ca.Certificate);
            var ca2Cert = new X509Certificate2(ca2.Certificate);

            return new CertificateWithCustomIntermediatesResult(
                        certificate,
                        new[] { caCert, ca2Cert },
                        responders);
        }

        public async Task<CertificateWithCustomIntermediatesResult> GetPartialChainAndRevokedSigningCertificateAsync()
        {
            var testServer = await GetTestServerAsync();

            var ca = await GetCertificateAuthorityAsync();
            var ca2 = ca.CreateIntermediateCertificateAuthority();
            var responders = new DisposableList<IDisposable>();

            responders.Add(testServer.RegisterResponder(ca2.OcspResponder));

            void CustomizeAsPartialChainAndRevokedCertificate(CertificateRequest certificateRequest)
            {
                certificateRequest.AddSigningEku();
                certificateRequest.AddAuthorityInfoAccess(ca2, addOcsp: true, addCAIssuers: true);
            }

            var (publicCertificate, certificate) = IssueCertificate(ca2, "Untrusted and Revoked Signing", CustomizeAsPartialChainAndRevokedCertificate);

            ca2.Revoke(publicCertificate, reason: X509RevocationReason.Unspecified, revocationDate: DateTimeOffset.UtcNow);

            return new CertificateWithCustomIntermediatesResult(
                        certificate,
                        Array.Empty<X509Certificate2>(),
                        responders);
        }

        public async Task<X509Certificate2> GetExpiredSigningCertificateAsync()
        {
            var ca = await GetCertificateAuthorityAsync();

            void CustomizeAsExpiredCertificate(CertificateRequest certificateRequest)
            {
                certificateRequest.AddSigningEku();
                certificateRequest.AddAuthorityInfoAccess(ca, addOcsp: true, addCAIssuers: true);
            }

            DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddHours(-1);
            DateTimeOffset notAfter = DateTimeOffset.UtcNow.AddHours(-1);

            var (publicCertificate, certificate) = IssueCertificate(ca, "Expired Signing", CustomizeAsExpiredCertificate, notBefore, notAfter);

            return certificate;
        }

        public async Task<X509Certificate2> GetExpiredAndRevokedSigningCertificateAsync()
        {
            var ca = await GetCertificateAuthorityAsync();

            void CustomizeAsExpiredAndRevokedCertificate(CertificateRequest certificateRequest)
            {
                certificateRequest.AddSigningEku();
                certificateRequest.AddAuthorityInfoAccess(ca, addOcsp: true, addCAIssuers: true);
            }

            DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddHours(-1);
            DateTimeOffset notAfter = DateTimeOffset.UtcNow.AddHours(-1);

            var (publicCertificate, certificate) = IssueCertificate(ca, "Expired Signing", CustomizeAsExpiredAndRevokedCertificate, notBefore, notAfter);

            ca.Revoke(publicCertificate, reason: X509RevocationReason.Unspecified, revocationDate: DateTimeOffset.UtcNow);

            return certificate;
        }

        public async Task<X509Certificate2> GetWeakSignatureParentSigningCertificateAsync()
        {
            var testServer = await GetTestServerAsync();
            var rootCa = await GetRootCertificateAuthorityAsync();

            var issueOptions = IssueCertificateOptions.CreateDefaultForIntermediateCertificateAuthority();
            var keyPair = SigningTestUtility.GenerateKeyPair(publicKeyLength: 512);

            issueOptions.KeyPair = keyPair;

            var intermediateCa = rootCa.CreateIntermediateCertificateAuthority(issueOptions);
            var responders = GetResponders();

            responders.AddRange(testServer.RegisterResponders(intermediateCa));

            return CreateSigningCertificate(intermediateCa);
        }

        public class CertificateWithCustomIntermediatesResult : IDisposable
        {
            private IDisposable _responders;

            public CertificateWithCustomIntermediatesResult(
                X509Certificate2 endCertificate,
                X509Certificate2[] intermediateCertificates,
                IDisposable responders)
            {
                EndCertificate = endCertificate;
                IntermediateCertificates = intermediateCertificates;
                _responders = responders;
            }

            public X509Certificate2 EndCertificate { get; }
            public X509Certificate2[] IntermediateCertificates { get; }

            public void Dispose() => _responders.Dispose();
        }
    }
}
