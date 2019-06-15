// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NuGet.Services.Validation;
using TestUtil;
using Validation.PackageSigning.ValidateCertificate.Tests.Support;
using Xunit;

namespace Validation.PackageSigning.ValidateCertificate.Tests
{
    using CertificateIntegrationTestFixure = Support.CertificateIntegrationTestFixture;

    [Collection(CertificateIntegrationTestCollection.Name)]
    public class OnlineCertificateVerifierIntegrationTests
    {
        private readonly CertificateIntegrationTestFixure _fixture;
        private readonly OnlineCertificateVerifier _target;

        public OnlineCertificateVerifierIntegrationTests(CertificateIntegrationTestFixure fixture)
        {
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));

            _target = new OnlineCertificateVerifier();
        }

        [AdminOnlyFact]
        public async Task ValidCodeSigningCertificate()
        {
            // Arrange
            var certificate = await _fixture.GetSigningCertificateAsync();

            // Act & assert
            var result = _target.VerifyCodeSigningCertificate(certificate, new X509Certificate2[0]);

            Assert.Equal(EndCertificateStatus.Good, result.Status);
            Assert.Equal(X509ChainStatusFlags.NoError, result.StatusFlags);
            Assert.NotNull(result.StatusUpdateTime);
            Assert.Null(result.RevocationTime);
        }

        [AdminOnlyFact]
        public async Task ValidTimestampingCertificate()
        {
            // Arrange
            var certificate = await _fixture.GetTimestampingCertificateAsync();

            // Act & assert
            var result = _target.VerifyTimestampingCertificate(certificate, new X509Certificate2[0]);

            Assert.Equal(EndCertificateStatus.Good, result.Status);
            Assert.Equal(X509ChainStatusFlags.NoError, result.StatusFlags);
            Assert.NotNull(result.StatusUpdateTime);
            Assert.Null(result.RevocationTime);
        }

        [AdminOnlyFact]
        public async Task ValidCodeSigningCertificateFailsAsTimestampingCertificate()
        {
            // Arrange
            var certificate = await _fixture.GetSigningCertificateAsync();

            // Act & assert
            var result = _target.VerifyTimestampingCertificate(certificate, new X509Certificate2[0]);

            Assert.Equal(EndCertificateStatus.Invalid, result.Status);
            Assert.Equal(X509ChainStatusFlags.NotValidForUsage, result.StatusFlags);
            Assert.NotNull(result.StatusUpdateTime);
            Assert.Null(result.RevocationTime);
        }

        [AdminOnlyFact]
        public async Task ValidTimestampingCertificateFailsAsSigningCertificate()
        {
            // Arrange
            var certificate = await _fixture.GetTimestampingCertificateAsync();

            // Act & assert
            var result = _target.VerifyCodeSigningCertificate(certificate, new X509Certificate2[0]);

            Assert.Equal(EndCertificateStatus.Invalid, result.Status);
            Assert.Equal(X509ChainStatusFlags.NotValidForUsage, result.StatusFlags);
            Assert.NotNull(result.StatusUpdateTime);
            Assert.Null(result.RevocationTime);
        }

        [AdminOnlyFact]
        public async Task UnknownCertificate()
        {
            // Arrange
            var certificate = await _fixture.GetUnknownSigningCertificateAsync();

            // Act & assert
            var result = _target.VerifyCodeSigningCertificate(certificate, new X509Certificate2[0]);

            Assert.Equal(EndCertificateStatus.Unknown, result.Status);
            Assert.Equal(X509ChainStatusFlags.RevocationStatusUnknown, result.StatusFlags);
            Assert.Null(result.StatusUpdateTime);
            Assert.Null(result.RevocationTime);
        }

        [AdminOnlyFact]
        public async Task RevokedEndCertificate()
        {
            // Arrange - create a revocation date with no milliseconds
            var crlUpdateTime = DateTimeOffset.Parse("10/21/1995 4:05:06 PM");
            var revocationDate = DateTimeOffset.Parse("02/18/1994 1:02:03 PM");

            using (var revokedChain = await _fixture.GetRevokedSigningCertificateAsync(revocationDate, crlUpdateTime))
            {
                // Act & assert
                var result = _target.VerifyCodeSigningCertificate(revokedChain.EndCertificate, new X509Certificate2[0]);

                Assert.Equal(EndCertificateStatus.Revoked, result.Status);
                Assert.Equal(X509ChainStatusFlags.Revoked, result.StatusFlags);
                Assert.NotNull(result.StatusUpdateTime);
                Assert.NotNull(result.RevocationTime);
                Assert.Equal(crlUpdateTime, result.StatusUpdateTime.Value);
                Assert.Equal(revocationDate, result.RevocationTime.Value);
            }
        }

        [AdminOnlyFact]
        public async Task RevokedParentCertificate()
        {
            // Arrange
            var certificate = await _fixture.GetRevokedParentSigningCertificateAsync();

            // Act & assert
            var result = _target.VerifyCodeSigningCertificate(certificate, new X509Certificate2[0]);

            // The intermediary is revoked, thus the end certificate should have an unknown
            // revocation status that is offline.
            var flags = X509ChainStatusFlags.Revoked |
                        X509ChainStatusFlags.RevocationStatusUnknown |
                        X509ChainStatusFlags.OfflineRevocation;

            Assert.Equal(EndCertificateStatus.Invalid, result.Status);
            Assert.Equal(flags, result.StatusFlags);
            Assert.Null(result.StatusUpdateTime);
            Assert.Null(result.RevocationTime);
        }

        [AdminOnlyFact]
        public async Task PartialChainEndCertificate()
        {
            // Arrange
            using (var partialChainResult = await _fixture.GetPartialChainSigningCertificateAsync())
            {
                // Act & assert
                var result = _target.VerifyCodeSigningCertificate(
                                partialChainResult.EndCertificate,
                                new X509Certificate2[0]);

                var flags = X509ChainStatusFlags.PartialChain |
                            X509ChainStatusFlags.RevocationStatusUnknown |
                            X509ChainStatusFlags.OfflineRevocation;

                Assert.Equal(EndCertificateStatus.Invalid, result.Status);
                Assert.Equal(flags, result.StatusFlags);
                Assert.Null(result.StatusUpdateTime);
                Assert.Null(result.RevocationTime);
            }
        }

        [AdminOnlyFact]
        public async Task PartialChainButIntermediateCertificateIsProvided()
        {
            // Arrange
            using (var partialChainResult = await _fixture.GetPartialChainSigningCertificateAsync())
            {
                // Act & assert
                var result = _target.VerifyCodeSigningCertificate(
                                partialChainResult.EndCertificate,
                                partialChainResult.IntermediateCertificates);

                Assert.Equal(EndCertificateStatus.Good, result.Status);
                Assert.Equal(X509ChainStatusFlags.NoError, result.StatusFlags);
                Assert.NotNull(result.StatusUpdateTime);
                Assert.Null(result.RevocationTime);
            }
        }

        [AdminOnlyFact]
        public async Task PartialChainAndRevokedEndCertificate()
        {
            // Arrange
            using (var partialChainResult = await _fixture.GetPartialChainAndRevokedSigningCertificateAsync())
            {
                // Act & assert
                var result = _target.VerifyCodeSigningCertificate(
                                partialChainResult.EndCertificate,
                                new X509Certificate2[0]);

                var flags = X509ChainStatusFlags.PartialChain |
                            X509ChainStatusFlags.RevocationStatusUnknown |
                            X509ChainStatusFlags.OfflineRevocation;

                Assert.Equal(EndCertificateStatus.Invalid, result.Status);
                Assert.Equal(flags, result.StatusFlags);
                Assert.Null(result.StatusUpdateTime);
                Assert.Null(result.RevocationTime);
            }
        }

        [AdminOnlyFact]
        public async Task ExpiredEndCertificate()
        {
            // Arrange
            var certificate = await _fixture.GetExpiredSigningCertificateAsync();

            // Act & assert
            var result = _target.VerifyCodeSigningCertificate(certificate, new X509Certificate2[0]);

            Assert.Equal(EndCertificateStatus.Invalid, result.Status);
            Assert.Equal(X509ChainStatusFlags.NotTimeValid, result.StatusFlags);
            Assert.NotNull(result.StatusUpdateTime);
            Assert.Null(result.RevocationTime);
        }

        [AdminOnlyFact]
        public async Task ExpiredAndRevokedEndCertificate()
        {
            // Arrange
            var certificate = await _fixture.GetExpiredAndRevokedSigningCertificateAsync();

            // Act & assert
            var result = _target.VerifyCodeSigningCertificate(certificate, new X509Certificate2[0]);

            var flags = X509ChainStatusFlags.NotTimeValid | X509ChainStatusFlags.Revoked;

            Assert.Equal(EndCertificateStatus.Invalid, result.Status);
            Assert.Equal(flags, result.StatusFlags);
            Assert.NotNull(result.StatusUpdateTime);
            Assert.NotNull(result.RevocationTime);
        }

        [AdminOnlyFact]
        public async Task WeakSignatureParentCertificate()
        {
            // Arrange
            var certificate = await _fixture.GetWeakSignatureParentSigningCertificateAsync();

            // Act & assert
            var result = _target.VerifyCodeSigningCertificate(certificate, new X509Certificate2[0]);

            var flags = X509ChainStatusFlags.HasWeakSignature | X509ChainStatusFlags.NotSignatureValid;

            Assert.Equal(EndCertificateStatus.Invalid, result.Status);
            Assert.Equal(flags, result.StatusFlags);
            Assert.NotNull(result.StatusUpdateTime);
            Assert.Null(result.RevocationTime);
        }
    }
}
