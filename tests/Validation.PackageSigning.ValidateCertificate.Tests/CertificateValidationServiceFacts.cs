// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Services.Validation;
using Tests.ContextHelpers;
using Xunit;

namespace Validation.PackageSigning.ValidateCertificate.Tests
{
    public class CertificateValidationServiceFacts
    {
        public const int EndCertificateKey1 = 111;
        public const int EndCertificateKey2 = 222;

        public static readonly Guid ValidationId1 = Guid.Empty;
        public static readonly Guid ValidationId2 = new Guid("fb9c0bac-3d4d-4cc7-ac2d-b3940e15b94d");

        public class TheFindCertificateValidationAsyncMethod : FactsBase
        {
            [Fact]
            public async Task ReturnsNullIfCertificateValidationDoesntExist()
            {
                // Arrange
                _context.Mock(
                    certificateValidations: new[]
                    {
                        _certificateValidation1,
                        _certificateValidation2,
                    }
                );

                var message = new CertificateValidationMessage(
                    certificateKey: EndCertificateKey1,
                    validationId: ValidationId2);

                // Act & Assert
                var result = await _target.FindCertificateValidationAsync(message);

                Assert.Null(result);
            }

            [Fact]
            public async Task ReturnsCertificateValidationIfExists()
            {
                // Arrange
                _context.Mock(
                    certificateValidations: new[]
                    {
                        _certificateValidation1,
                        _certificateValidation2,
                    }
                );

                var message = new CertificateValidationMessage(
                    certificateKey: EndCertificateKey2,
                    validationId: ValidationId2);

                // Act & Assert
                var result = await _target.FindCertificateValidationAsync(message);

                Assert.NotNull(result);
                Assert.Equal(EndCertificateKey2, result.EndCertificateKey);
                Assert.Equal(ValidationId2, result.ValidationId);
            }
        }

        public class TheTrySaveResultAsyncMethod : FactsBase
        {
            [Fact]
            public async Task GoodResultUpdatesCertificateValidation()
            {
                // Arrange
                var verificationResult = new CertificateVerificationResult(
                                                status: EndCertificateStatus.Good,
                                                statusFlags: X509ChainStatusFlags.NoError);

                // Act & Assert
                var result = await _target.TrySaveResultAsync(_certificateValidation1, verificationResult);

                Assert.True(result);
                Assert.Equal(EndCertificateStatus.Good, _certificateValidation1.Status);
                Assert.Equal(EndCertificateStatus.Good, _certificateValidation1.EndCertificate.Status);
                Assert.Equal(0, _certificateValidation1.EndCertificate.ValidationFailures);
                Assert.Null(_certificateValidation1.EndCertificate.RevocationTime);
            }

            [Fact]
            public async Task InvalidResultInvalidatesDependentSignatures()
            {
                // Arrange - Invalidate a certificate that is depended on by "signature1"'s certificate.
                // This should result in "signature1" being invalidated.
                var verificationResult = new CertificateVerificationResult(
                                                status: EndCertificateStatus.Invalid,
                                                statusFlags: X509ChainStatusFlags.ExplicitDistrust);

                var signingState = new PackageSigningState { SigningStatus = PackageSigningStatus.Valid };
                var signature1 = new PackageSignature
                {
                    Key = 123,
                    Status = PackageSignatureStatus.Valid,
                    Type = PackageSignatureType.Author,
                };
                var signature2 = new PackageSignature
                {
                    Key = 456,
                    Status = PackageSignatureStatus.Valid,
                    Type = PackageSignatureType.Author,
                };
                var timestamp = new TrustedTimestamp { Value = DateTime.UtcNow };

                signingState.PackageSignatures = new[] { signature1, signature2 };
                signature1.PackageSigningState = signingState;
                signature2.PackageSigningState = signingState;
                signature1.EndCertificate = _certificate1;
                signature2.EndCertificate = _certificate2;
                signature1.TrustedTimestamps = new TrustedTimestamp[0];
                signature2.TrustedTimestamps = new[] { timestamp };
                timestamp.PackageSignature = signature2;
                timestamp.EndCertificate = _certificate1;
                _certificate1.PackageSignatures = new[] { signature1 };
                _certificate1.TrustedTimestamps = new[] { timestamp };
                _certificate1.Use = EndCertificateUse.CodeSigning;

                _context.Mock(
                    packageSignatures: new[] { signature1, signature2 },
                    trustedTimestamps: new[] { timestamp });

                // Act
                var result = await _target.TrySaveResultAsync(_certificateValidation1, verificationResult);

                Assert.True(result);

                Assert.Equal(EndCertificateStatus.Invalid, _certificateValidation1.Status);

                Assert.Equal(EndCertificateStatus.Invalid, _certificate1.Status);
                Assert.Equal(0, _certificate1.ValidationFailures);
                Assert.Null(_certificate1.RevocationTime);

                Assert.Equal(PackageSignatureStatus.Invalid, signature1.Status);
                Assert.Equal(PackageSignatureStatus.Valid, signature2.Status);

                Assert.Equal(PackageSigningStatus.Invalid, signingState.SigningStatus);

                // The package's signing state is "Valid", a MayBeInvalidated (warn) event should be raised.
                _telemetryService.Verify(a => a.TrackUnableToValidateCertificateEvent(It.IsAny<EndCertificate>()), Times.Never);
                _telemetryService.Verify(a => a.TrackPackageSignatureMayBeInvalidatedEvent(It.IsAny<PackageSignature>()), Times.Exactly(1));
                _telemetryService.Verify(a => a.TrackPackageSignatureShouldBeInvalidatedEvent(It.IsAny<PackageSignature>()), Times.Exactly(0));
                _context.Verify(c => c.SaveChangesAsync(), Times.Once);
            }

            [Theory]
            [InlineData(PackageSignatureType.Repository)]
            [InlineData((PackageSignatureType)0)]
            public async Task RevokedResultDoesNotInvalidateDependentNonAuthorSignaturesSignatures(PackageSignatureType type)
            {
                // Arrange
                var revocationTime = DateTime.UtcNow;

                var verificationResult = new CertificateVerificationResult(
                                                status: EndCertificateStatus.Revoked,
                                                statusFlags: X509ChainStatusFlags.Revoked,
                                                revocationTime: revocationTime);

                var signingState = new PackageSigningState { SigningStatus = PackageSigningStatus.Valid };
                var signature2 = new PackageSignature { Key = 12, Status = PackageSignatureStatus.Valid, Type = type };
                var timestamp2 = new TrustedTimestamp { Value = revocationTime.AddDays(1), Status = TrustedTimestampStatus.Valid };

                signingState.PackageSignatures = new[] { signature2 };
                signature2.PackageSigningState = signingState;
                signature2.EndCertificate = _certificate1;
                signature2.TrustedTimestamps = new[] { timestamp2 };
                timestamp2.PackageSignature = signature2;
                timestamp2.EndCertificate = _certificate2;
                _certificate1.Use = EndCertificateUse.CodeSigning;
                _certificate2.Use = EndCertificateUse.Timestamping;
                _certificate1.PackageSignatures = new[] { signature2 };
                _certificate2.TrustedTimestamps = new[] { timestamp2 };

                _context.Mock(
                    packageSigningStates: new[] { signingState },
                    packageSignatures: new[] { signature2 },
                    trustedTimestamps: new[] { timestamp2 },
                    endCertificates: new[] { _certificate1, _certificate2 });

                var result = await _target.TrySaveResultAsync(_certificateValidation1, verificationResult);

                Assert.True(result);

                Assert.Equal(EndCertificateStatus.Revoked, _certificateValidation1.Status);

                Assert.Equal(EndCertificateStatus.Revoked, _certificate1.Status);
                Assert.Equal(0, _certificate1.ValidationFailures);
                Assert.Equal(revocationTime, _certificate1.RevocationTime);

                Assert.Equal(PackageSignatureStatus.Valid, signature2.Status);

                Assert.Equal(PackageSigningStatus.Valid, signingState.SigningStatus);

                _telemetryService.Verify(a => a.TrackUnableToValidateCertificateEvent(It.IsAny<EndCertificate>()), Times.Never);
                _telemetryService.Verify(a => a.TrackPackageSignatureShouldBeInvalidatedEvent(It.IsAny<PackageSignature>()), Times.Never);
                _telemetryService.Verify(a => a.TrackUnableToValidateCertificateEvent(It.IsAny<EndCertificate>()), Times.Never);
                _context.Verify(c => c.SaveChangesAsync(), Times.Once);
            }

            [Fact]
            public async Task RevokedResultInvalidatesDependentSignatures()
            {
                // Arrange - "signature1" is a signature that uses the certificate before the revocation date,
                // "signature2" is a signature that uses the certificate after the revocation date, "signature3"
                // is a signature that doesn't depend on the certificate.
                var revocationTime = DateTime.UtcNow;

                var verificationResult = new CertificateVerificationResult(
                                                status: EndCertificateStatus.Revoked,
                                                statusFlags: X509ChainStatusFlags.Revoked,
                                                revocationTime: revocationTime);

                var signingState = new PackageSigningState { SigningStatus = PackageSigningStatus.Valid };
                var signature1 = new PackageSignature
                {
                    Key = 12,
                    Status = PackageSignatureStatus.Valid,
                    Type = PackageSignatureType.Author,
                };
                var signature2 = new PackageSignature
                {
                    Key = 23,
                    Status = PackageSignatureStatus.Valid,
                    Type = PackageSignatureType.Author,
                };
                var signature3 = new PackageSignature
                {
                    Key = 34,
                    Status = PackageSignatureStatus.Valid,
                    Type = PackageSignatureType.Author,
                };
                var timestamp1 = new TrustedTimestamp { Value = revocationTime.AddDays(-1), Status = TrustedTimestampStatus.Valid };
                var timestamp2 = new TrustedTimestamp { Value = revocationTime.AddDays(1), Status = TrustedTimestampStatus.Valid };
                var timestamp3 = new TrustedTimestamp { Value = revocationTime.AddDays(-1), Status = TrustedTimestampStatus.Valid };

                signingState.PackageSignatures = new[] { signature1, signature2, signature3 };
                signature1.PackageSigningState = signingState;
                signature2.PackageSigningState = signingState;
                signature3.PackageSigningState = signingState;
                signature1.EndCertificate = _certificate1;
                signature2.EndCertificate = _certificate1;
                signature3.EndCertificate = _certificate2;
                signature1.TrustedTimestamps = new[] { timestamp1 };
                signature2.TrustedTimestamps = new[] { timestamp2 };
                signature3.TrustedTimestamps = new[] { timestamp3 };
                timestamp1.PackageSignature = signature1;
                timestamp2.PackageSignature = signature2;
                timestamp3.PackageSignature = signature3;
                timestamp1.EndCertificate = _certificate2;
                timestamp2.EndCertificate = _certificate2;
                timestamp3.EndCertificate = _certificate2;
                _certificate1.Use = EndCertificateUse.CodeSigning;
                _certificate2.Use = EndCertificateUse.Timestamping;
                _certificate1.PackageSignatures = new[] { signature1, signature2 };
                _certificate2.TrustedTimestamps = new[] { timestamp1, timestamp2, timestamp3 };

                _context.Mock(
                    packageSigningStates: new[] { signingState },
                    packageSignatures: new[] { signature1, signature2, signature3 },
                    trustedTimestamps: new[] { timestamp1, timestamp2, timestamp3 },
                    endCertificates: new[] { _certificate1, _certificate2 });

                var result = await _target.TrySaveResultAsync(_certificateValidation1, verificationResult);

                Assert.True(result);

                Assert.Equal(EndCertificateStatus.Revoked, _certificateValidation1.Status);

                Assert.Equal(EndCertificateStatus.Revoked, _certificate1.Status);
                Assert.Equal(0, _certificate1.ValidationFailures);
                Assert.Equal(revocationTime, _certificate1.RevocationTime);

                Assert.Equal(PackageSignatureStatus.Valid, signature1.Status);
                Assert.Equal(PackageSignatureStatus.Invalid, signature2.Status);
                Assert.Equal(PackageSignatureStatus.Valid, signature3.Status);

                Assert.Equal(PackageSigningStatus.Invalid, signingState.SigningStatus);

                _telemetryService.Verify(a => a.TrackUnableToValidateCertificateEvent(It.IsAny<EndCertificate>()), Times.Never);
                _telemetryService.Verify(a => a.TrackPackageSignatureShouldBeInvalidatedEvent(It.IsAny<PackageSignature>()), Times.Exactly(1));
                _context.Verify(c => c.SaveChangesAsync(), Times.Once);
            }

            [Fact]
            public async Task RevokedResultInvalidatesDependentTimestamps()
            {
                // Arrange - "signature1" is a signature that whose timestamp depends on the certificate that will be revoked.
                var verificationResult = new CertificateVerificationResult(
                                                status: EndCertificateStatus.Revoked,
                                                statusFlags: X509ChainStatusFlags.Revoked,
                                                revocationTime: DateTime.UtcNow);

                var signingState = new PackageSigningState { SigningStatus = PackageSigningStatus.Valid };
                var signature1 = new PackageSignature
                {
                    Key = 12,
                    Status = PackageSignatureStatus.Valid,
                    Type = PackageSignatureType.Author,
                };
                var timestamp1 = new TrustedTimestamp { Value = DateTime.UtcNow, Status = TrustedTimestampStatus.Valid };

                signingState.PackageSignatures = new[] { signature1 };
                signature1.PackageSigningState = signingState;
                signature1.EndCertificate = _certificate1;
                signature1.TrustedTimestamps = new[] { timestamp1 };
                timestamp1.PackageSignature = signature1;
                timestamp1.EndCertificate = _certificate1;
                _certificate1.Use = EndCertificateUse.Timestamping;
                _certificate1.PackageSignatures = new[] { signature1 };

                _context.Mock(
                    packageSigningStates: new[] { signingState },
                    packageSignatures: new[] { signature1 },
                    trustedTimestamps: new[] { timestamp1 },
                    endCertificates: new[] { _certificate1 });

                var result = await _target.TrySaveResultAsync(_certificateValidation1, verificationResult);

                Assert.True(result);

                Assert.Equal(EndCertificateStatus.Revoked, _certificateValidation1.Status);
                Assert.Equal(TrustedTimestampStatus.Invalid, timestamp1.Status);
                Assert.Equal(EndCertificateStatus.Revoked, _certificate1.Status);
                Assert.Equal(PackageSignatureStatus.Invalid, signature1.Status);
                Assert.Equal(PackageSigningStatus.Invalid, signingState.SigningStatus);
            }

            [Fact]
            public async Task UnknownResultUpdatesCertificateValidation()
            {
                // Arrange - Create a signature whose certificate and trusted timestamp depends on "_certificateValidation1".
                var verificationResult = new CertificateVerificationResult(
                                                status: EndCertificateStatus.Unknown,
                                                statusFlags: X509ChainStatusFlags.RevocationStatusUnknown);

                var signature = new PackageSignature
                {
                    Status = PackageSignatureStatus.Valid,
                    Type = PackageSignatureType.Author,
                };
                var timestamp = new TrustedTimestamp { Value = DateTime.UtcNow };

                signature.EndCertificate = _certificate1;
                signature.TrustedTimestamps = new[] { timestamp };
                _certificate1.PackageSignatures = new[] { signature };
                _certificate1.TrustedTimestamps = new[] { timestamp };

                // Act & Assert - the first Unknown result shouldn't cause any issues.
                var result = await _target.TrySaveResultAsync(_certificateValidation1, verificationResult);

                Assert.True(result);
                Assert.Null(_certificateValidation1.Status);
                Assert.Equal(EndCertificateStatus.Unknown, _certificateValidation1.EndCertificate.Status);
                Assert.Equal(4, _certificateValidation1.EndCertificate.ValidationFailures);
                Assert.Null(_certificateValidation1.EndCertificate.RevocationTime);

                _telemetryService.Verify(a => a.TrackUnableToValidateCertificateEvent(It.IsAny<EndCertificate>()), Times.Never);
                _telemetryService.Verify(a => a.TrackPackageSignatureShouldBeInvalidatedEvent(It.IsAny<PackageSignature>()), Times.Never);
                _context.Verify(c => c.SaveChangesAsync(), Times.Once);
            }

            [Fact]
            public async Task UnknownResultAlertsIfReachesMaxFailureThreshold()
            {
                // Arrange - Create a signature whose certificate and trusted timestamp depends on "_certificateValidation1".
                var verificationResult = new CertificateVerificationResult(
                                                status: EndCertificateStatus.Unknown,
                                                statusFlags: X509ChainStatusFlags.RevocationStatusUnknown);

                var signature = new PackageSignature
                {
                    Status = PackageSignatureStatus.Valid,
                    Type = PackageSignatureType.Author,
                };
                var timestamp = new TrustedTimestamp { Value = DateTime.UtcNow };

                signature.EndCertificate = _certificate1;
                signature.TrustedTimestamps = new[] { timestamp };
                _certificate1.PackageSignatures = new[] { signature };
                _certificate1.TrustedTimestamps = new[] { timestamp };

                // Act & Assert - the first Unknown result shouldn't cause any issues.
                var result = await _target.TrySaveResultAsync(_certificateValidation1, verificationResult);

                Assert.True(result);
                Assert.Null(_certificateValidation1.Status);
                Assert.Equal(EndCertificateStatus.Unknown, _certificateValidation1.EndCertificate.Status);
                Assert.Equal(4, _certificateValidation1.EndCertificate.ValidationFailures);
                Assert.Null(_certificateValidation1.EndCertificate.RevocationTime);

                // The second result should trigger an alert but should NOT invalidate signatures.
                result = await _target.TrySaveResultAsync(_certificateValidation1, verificationResult);

                Assert.True(result);
                Assert.Equal(EndCertificateStatus.Invalid, _certificateValidation1.Status);
                Assert.Equal(EndCertificateStatus.Invalid, _certificateValidation1.EndCertificate.Status);
                Assert.Equal(5, _certificateValidation1.EndCertificate.ValidationFailures);
                Assert.Null(_certificateValidation1.EndCertificate.RevocationTime);

                _telemetryService.Verify(a => a.TrackUnableToValidateCertificateEvent(It.IsAny<EndCertificate>()), Times.Once);
                _telemetryService.Verify(a => a.TrackPackageSignatureShouldBeInvalidatedEvent(It.IsAny<PackageSignature>()), Times.Never);
                _context.Verify(c => c.SaveChangesAsync(), Times.Exactly(2));
            }

            [Fact]
            public async Task ProcessesSignaturesInBatchesOf500()
            {
                // Arrange - Invalidate a certificate that is depended on by 501 signatures.
                // This should invalidate all signatures in batches of 500 signatures.
                var verificationResult = new CertificateVerificationResult(
                    status: EndCertificateStatus.Invalid,
                    statusFlags: X509ChainStatusFlags.ExplicitDistrust);

                var signatures = new List<PackageSignature>();

                for (var i = 0; i < 501; i++)
                {
                    var state = new PackageSigningState { SigningStatus = PackageSigningStatus.Valid };

                    var signature = new PackageSignature
                    {
                        Key = i,
                        Status = PackageSignatureStatus.Valid,
                        Type = PackageSignatureType.Author
                    };

                    state.PackageSignatures = new[] { signature };
                    signature.PackageSigningState = state;
                    signature.EndCertificate = _certificate1;
                    signature.TrustedTimestamps = new TrustedTimestamp[0];

                    signatures.Add(signature);
                }

                _certificate1.PackageSignatures = signatures;
                _certificate1.TrustedTimestamps = new TrustedTimestamp[0];
                _certificate1.Use = EndCertificateUse.CodeSigning;

                _context.Mock(packageSignatures: signatures);

                var hasSaved = false;
                var invalidationsBeforeSave = 0;
                var invalidationsAfterSave = 0;

                _context.Setup(c => c.SaveChangesAsync())
                    .Returns(Task.FromResult(0))
                    .Callback(() => hasSaved = true);

                _telemetryService
                    .Setup(t => t.TrackPackageSignatureMayBeInvalidatedEvent(It.IsAny<PackageSignature>()))
                    .Callback((PackageSignature s) =>
                    {
                        if (!hasSaved)
                        {
                            invalidationsBeforeSave++;
                        }
                        else
                        {
                            invalidationsAfterSave++;
                        }
                    });

                // Act
                var result = await _target.TrySaveResultAsync(_certificateValidation1, verificationResult);

                // Assert - two batches should be saved. The first batch should invalidate 500 signatures,
                // the second batch should invalidate 1 signature.
                _context.Verify(c => c.SaveChangesAsync(), Times.Exactly(2));

                Assert.Equal(500, invalidationsBeforeSave);
                Assert.Equal(1, invalidationsAfterSave);
            }
        }

        public class FactsBase
        {
            protected readonly Mock<IValidationEntitiesContext> _context;
            protected readonly Mock<ITelemetryService> _telemetryService;

            protected readonly EndCertificate _certificate1 = new EndCertificate
            {
                Key = EndCertificateKey1,
                Thumbprint = "Certificate 1 Thumbprint",
                Status = EndCertificateStatus.Unknown,
                ValidationFailures = 3,
                CertificateChainLinks = new CertificateChainLink[0],
            };

            protected readonly EndCertificate _certificate2 = new EndCertificate
            {
                Key = EndCertificateKey2,
                Thumbprint = "Certificate 2 Thumbprint",
                Status = EndCertificateStatus.Unknown,
                ValidationFailures = 3,
                CertificateChainLinks = new CertificateChainLink[0],
            };

            protected readonly EndCertificateValidation _certificateValidation1 = new EndCertificateValidation
            {
                Key = 123,
                EndCertificateKey = EndCertificateKey1,
                ValidationId = ValidationId1,
                Status = null,
            };

            protected readonly EndCertificateValidation _certificateValidation2 = new EndCertificateValidation
            {
                Key = 456,
                EndCertificateKey = EndCertificateKey2,
                ValidationId = ValidationId2,
                Status = null,
            };

            protected readonly CertificateValidationService _target;

            public FactsBase()
            {
                _context = new Mock<IValidationEntitiesContext>();
                _telemetryService = new Mock<ITelemetryService>();

                var logger = new Mock<ILogger<CertificateValidationService>>();

                _certificateValidation1.EndCertificate = _certificate1;
                _certificateValidation2.EndCertificate = _certificate2;

                _target = new CertificateValidationService(
                    _context.Object,
                    _telemetryService.Object,
                    logger.Object,
                    maximumValidationFailures: 5);
            }
        }
    }
}
