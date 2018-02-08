// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NuGet.Services.Validation;
using Validation.PackageSigning.Core.Tests.Support;
using Validation.PackageSigning.ValidateCertificate.Tests.Support;
using Xunit;

namespace Validation.PackageSigning.ValidateCertificate.Tests
{
    [Collection(CertificateIntegrationTestCollection.Name)]
    public class OnlineCertificateVerifierIntegrationTests
    {
        private readonly CertificateIntegrationTestFixture _fixture;
        private readonly OnlineCertificateVerifier _target;

        public OnlineCertificateVerifierIntegrationTests(CertificateIntegrationTestFixture fixture)
        {
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));

            _target = new OnlineCertificateVerifier();
        }

        [Fact]
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
    }
}
