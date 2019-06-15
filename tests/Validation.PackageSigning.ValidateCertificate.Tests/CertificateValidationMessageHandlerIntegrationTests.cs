// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Services.Validation;
using Tests.ContextHelpers;
using TestUtil;
using Validation.PackageSigning.ValidateCertificate.Tests.Support;
using Xunit;

namespace Validation.PackageSigning.ValidateCertificate.Tests
{
    [Collection(CertificateIntegrationTestCollection.Name)]
    public class CertificateValidationMessageHandlerIntegrationTests
    {
        private static readonly DateTime RevocationTime = DateTime.UtcNow.AddDays(-4);
        private static readonly DateTime BeforeRevocationTime = RevocationTime.AddDays(-1);
        private static readonly DateTime AfterRevocationTime = RevocationTime.AddDays(1);

        private readonly CertificateIntegrationTestFixture _fixture;

        private readonly Mock<IValidationEntitiesContext> _context;
        private readonly Mock<ITelemetryService> _telemetryService;
        private readonly Mock<ICertificateStore> _certificateStore;

        private readonly CertificateValidationMessageHandler _target;

        public CertificateValidationMessageHandlerIntegrationTests(CertificateIntegrationTestFixture fixture)
        {
            _fixture = fixture;

            _context = new Mock<IValidationEntitiesContext>();
            _telemetryService = new Mock<ITelemetryService>();
            _certificateStore = new Mock<ICertificateStore>();

            var certificateValidationService = new CertificateValidationService(
                _context.Object,
                _telemetryService.Object,
                Mock.Of<ILogger<CertificateValidationService>>());

            _target = new CertificateValidationMessageHandler(
                _certificateStore.Object,
                certificateValidationService,
                new OnlineCertificateVerifier(),
                Mock.Of<ILogger<CertificateValidationMessageHandler>>());
        }

        [AdminOnlyTheory]
        [MemberData(nameof(ValidateSigningCertificateData))]
        public async Task ValidateSigningCertificate(
            Func<CertificateIntegrationTestFixture, Task<X509Certificate2>> createCertificateFunc,
            DateTime signatureTime,
            EndCertificateStatus expectedCertificateStatus,
            PackageSignatureStatus expectedStatusForSignatureAtIngestion,
            PackageSignatureStatus expectedStatusForSignatureInGracePeriod,
            PackageSignatureStatus expectedStatusForSignatureAfterGracePeriod)
        {
            // Arrange
            var certificate = await createCertificateFunc(_fixture);

            var endCertificateKey = 123;
            var validationId = Guid.NewGuid();

            var packageSigningState1 = new PackageSigningState { SigningStatus = PackageSigningStatus.Valid };
            var packageSigningState2 = new PackageSigningState { SigningStatus = PackageSigningStatus.Valid };
            var packageSigningState3 = new PackageSigningState { SigningStatus = PackageSigningStatus.Valid };

            var signatureAtIngestion = new PackageSignature
            {
                Status = PackageSignatureStatus.Unknown,
                Type = PackageSignatureType.Author,
            };
            var signatureInGracePeriod = new PackageSignature
            {
                Status = PackageSignatureStatus.InGracePeriod,
                Type = PackageSignatureType.Author,
            };
            var signatureAfterGracePeriod = new PackageSignature
            {
                Status = PackageSignatureStatus.Valid,
                Type = PackageSignatureType.Author,
            };

            var trustedTimestamp1 = new TrustedTimestamp { Status = TrustedTimestampStatus.Valid, Value = signatureTime };
            var trustedTimestamp2 = new TrustedTimestamp { Status = TrustedTimestampStatus.Valid, Value = signatureTime };
            var trustedTimestamp3 = new TrustedTimestamp { Status = TrustedTimestampStatus.Valid, Value = signatureTime };

            var endCertificate = new EndCertificate
            {
                Key = endCertificateKey,
                Status = EndCertificateStatus.Unknown,
                Use = EndCertificateUse.CodeSigning,
                CertificateChainLinks = new CertificateChainLink[0],
            };

            var validation = new EndCertificateValidation
            {
                EndCertificateKey = endCertificateKey,
                ValidationId = validationId,
                Status = null,
                EndCertificate = endCertificate
            };

            signatureAtIngestion.PackageSigningState = packageSigningState1;
            signatureAtIngestion.EndCertificate = endCertificate;
            signatureAtIngestion.TrustedTimestamps = new[] { trustedTimestamp1 };
            signatureInGracePeriod.PackageSigningState = packageSigningState2;
            signatureInGracePeriod.EndCertificate = endCertificate;
            signatureInGracePeriod.TrustedTimestamps = new[] { trustedTimestamp2 };
            signatureAfterGracePeriod.PackageSigningState = packageSigningState3;
            signatureAfterGracePeriod.EndCertificate = endCertificate;
            signatureAfterGracePeriod.TrustedTimestamps = new[] { trustedTimestamp3 };

            _context.Mock(
                packageSignatures: new[] { signatureAtIngestion, signatureInGracePeriod, signatureAfterGracePeriod },
                endCertificates: new[] { endCertificate },
                certificateValidations: new EndCertificateValidation[] { validation });

            _certificateStore.Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                             .Returns(Task.FromResult(certificate));

            // Act
            await _target.HandleAsync(new CertificateValidationMessage(certificateKey: endCertificateKey, validationId: validationId));

            // Assert
            Assert.Equal(expectedCertificateStatus, validation.Status);
            Assert.Equal(expectedCertificateStatus, endCertificate.Status);
            Assert.Equal(expectedStatusForSignatureAtIngestion, signatureAtIngestion.Status);
            Assert.Equal(expectedStatusForSignatureInGracePeriod, signatureInGracePeriod.Status);
            Assert.Equal(expectedStatusForSignatureAfterGracePeriod, signatureAfterGracePeriod.Status);

            _context.Verify(c => c.SaveChangesAsync(), Times.Once);
        }

        public static IEnumerable<object[]> ValidateSigningCertificateData()
        {
            Func<CertificateIntegrationTestFixture, Task<X509Certificate2>> getGoodCert =
               (CertificateIntegrationTestFixture f) => f.GetSigningCertificateAsync();

            yield return ValidateSigningCertificateArguments(
                createCertificateFunc: getGoodCert,
                signatureTime: DateTime.UtcNow,
                expectedCertificateStatus: EndCertificateStatus.Good,
                expectedStatusForSignatureAtIngestion: PackageSignatureStatus.Unknown,
                expectedStatusForSignatureInGracePeriod: PackageSignatureStatus.InGracePeriod,
                expectedStatusForSignatureAfterGracePeriod: PackageSignatureStatus.Valid);

            Func<CertificateIntegrationTestFixture, Task<X509Certificate2>> getRevokedParentCert =
                (CertificateIntegrationTestFixture f) => f.GetRevokedParentSigningCertificateAsync();

            yield return ValidateSigningCertificateArguments(
                createCertificateFunc: getRevokedParentCert,
                signatureTime: DateTime.UtcNow,
                expectedCertificateStatus: EndCertificateStatus.Invalid,
                expectedStatusForSignatureAtIngestion: PackageSignatureStatus.Invalid,
                expectedStatusForSignatureInGracePeriod: PackageSignatureStatus.Invalid,
                expectedStatusForSignatureAfterGracePeriod: PackageSignatureStatus.Invalid);

            Func<CertificateIntegrationTestFixture, Task<X509Certificate2>> getWeakSignatureParentCert =
                (CertificateIntegrationTestFixture f) => f.GetWeakSignatureParentSigningCertificateAsync();

            yield return ValidateSigningCertificateArguments(
                createCertificateFunc: getWeakSignatureParentCert,
                signatureTime: DateTime.UtcNow,
                expectedCertificateStatus: EndCertificateStatus.Invalid,
                expectedStatusForSignatureAtIngestion: PackageSignatureStatus.Invalid,
                expectedStatusForSignatureInGracePeriod: PackageSignatureStatus.InGracePeriod,
                expectedStatusForSignatureAfterGracePeriod: PackageSignatureStatus.Valid);

            Func<CertificateIntegrationTestFixture, Task<X509Certificate2>> getRevokedCert =
                (CertificateIntegrationTestFixture f) => f.GetRevokedSigningCertificateAsync(RevocationTime);

            yield return ValidateSigningCertificateArguments(
                createCertificateFunc: getRevokedCert,
                signatureTime: AfterRevocationTime,
                expectedCertificateStatus: EndCertificateStatus.Revoked,
                expectedStatusForSignatureAtIngestion: PackageSignatureStatus.Invalid,
                expectedStatusForSignatureInGracePeriod: PackageSignatureStatus.Invalid,
                expectedStatusForSignatureAfterGracePeriod: PackageSignatureStatus.Invalid);

            yield return ValidateSigningCertificateArguments(
                createCertificateFunc: getRevokedCert,
                signatureTime: BeforeRevocationTime,
                expectedCertificateStatus: EndCertificateStatus.Revoked,
                expectedStatusForSignatureAtIngestion: PackageSignatureStatus.Invalid,
                expectedStatusForSignatureInGracePeriod: PackageSignatureStatus.InGracePeriod,
                expectedStatusForSignatureAfterGracePeriod: PackageSignatureStatus.Valid);
        }

        private static object[] ValidateSigningCertificateArguments(
            Func<CertificateIntegrationTestFixture, Task<X509Certificate2>> createCertificateFunc,
            DateTime signatureTime,
            EndCertificateStatus expectedCertificateStatus,
            PackageSignatureStatus expectedStatusForSignatureAtIngestion,
            PackageSignatureStatus expectedStatusForSignatureInGracePeriod,
            PackageSignatureStatus expectedStatusForSignatureAfterGracePeriod)
        {
            return new object[]
            {
                createCertificateFunc,
                signatureTime,
                expectedCertificateStatus,
                expectedStatusForSignatureAtIngestion,
                expectedStatusForSignatureInGracePeriod,
                expectedStatusForSignatureAfterGracePeriod,
            };
        }

        [AdminOnlyFact]
        public async Task ValidateTimestampingCertificate()
        {
            // Arrange
            var certificate = await _fixture.GetRevokedTimestampingCertificateAsync(RevocationTime);

            var endCertificateKey = 123;
            var validationId = Guid.NewGuid();

            var packageSigningState = new PackageSigningState { SigningStatus = PackageSigningStatus.Valid };

            var signatureAtIngestion = new PackageSignature
            {
                Status = PackageSignatureStatus.Unknown,
                Type = PackageSignatureType.Author,
            };
            var signatureInGracePeriod = new PackageSignature
            {
                Status = PackageSignatureStatus.InGracePeriod,
                Type = PackageSignatureType.Author,
            };
            var signatureAfterGracePeriod = new PackageSignature
            {
                Status = PackageSignatureStatus.Valid,
                Type = PackageSignatureType.Author,
            };

            var endCertificate = new EndCertificate
            {
                Key = endCertificateKey,
                Status = EndCertificateStatus.Unknown,
                Use = EndCertificateUse.Timestamping,
                CertificateChainLinks = new CertificateChainLink[0],
            };

            var trustedTimestamp = new TrustedTimestamp
            {
                EndCertificate = endCertificate,
                Status = TrustedTimestampStatus.Valid
            };

            var validation = new EndCertificateValidation
            {
                EndCertificateKey = endCertificateKey,
                ValidationId = validationId,
                Status = null,
                EndCertificate = endCertificate
            };

            signatureAtIngestion.PackageSigningState = packageSigningState;
            signatureAtIngestion.TrustedTimestamps = new[] { trustedTimestamp };
            signatureInGracePeriod.PackageSigningState = packageSigningState;
            signatureInGracePeriod.TrustedTimestamps = new[] { trustedTimestamp };
            signatureAfterGracePeriod.PackageSigningState = packageSigningState;
            signatureAfterGracePeriod.TrustedTimestamps = new[] { trustedTimestamp };

            _context.Mock(
                packageSignatures: new[] { signatureAtIngestion, signatureInGracePeriod, signatureAfterGracePeriod },
                endCertificates: new[] { endCertificate },
                certificateValidations: new EndCertificateValidation[] { validation });

            _certificateStore.Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                             .Returns(Task.FromResult(certificate));

            // Act
            await _target.HandleAsync(new CertificateValidationMessage(certificateKey: endCertificateKey, validationId: validationId));

            // Assert
            Assert.Equal(EndCertificateStatus.Revoked, validation.Status);
            Assert.Equal(EndCertificateStatus.Revoked, endCertificate.Status);
            Assert.Equal(PackageSignatureStatus.Invalid, signatureAtIngestion.Status);
            Assert.Equal(PackageSignatureStatus.Invalid, signatureInGracePeriod.Status);
            Assert.Equal(PackageSignatureStatus.Invalid, signatureAfterGracePeriod.Status);

            _context.Verify(c => c.SaveChangesAsync(), Times.Once);
        }
    }
}
