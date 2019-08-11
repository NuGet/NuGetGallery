// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Services.Validation;
using Xunit;

namespace Validation.PackageSigning.ValidateCertificate.Tests
{
    public class CertificateValidationMessageHandlerFacts
    {
        public const long CertificateKey = 123;

        public static readonly Guid ValidationId = new Guid("fb9c0bac-3d4d-4cc7-ac2d-b3940e15b94d");

        public sealed class TheHandleAsyncMethod : FactsBase
        {
            [Fact]
            public async Task RetriesIfCertificateValidationDoesntExist()
            {
                // Arrange
                _certificateValidationService
                    .Setup(s => s.FindCertificateValidationAsync(It.IsAny<CertificateValidationMessage>()))
                    .ReturnsAsync((EndCertificateValidation)null);

                // Act & Assert
                Assert.False(await _target.HandleAsync(_message));

                _certificateValidationService.Verify(s => s.TrySaveResultAsync(It.IsAny<EndCertificateValidation>(), It.IsAny<CertificateVerificationResult>()), Times.Never);
            }

            [Fact]
            public async Task ConsumesMessageIfCertificateValidationEntityIsInvalid()
            {
                // Arrange
                _certificateValidationService
                    .Setup(s => s.FindCertificateValidationAsync(It.IsAny<CertificateValidationMessage>()))
                    .ReturnsAsync(new EndCertificateValidation
                    {
                        Status = EndCertificateStatus.Good,
                        EndCertificate = new EndCertificate(),
                    });

                // Act & Assert
                // The certificate validation that was found should have a null Status, but instead its Status is "Good".
                // The message handler should consume the message without performing any additional work.
                Assert.True(await _target.HandleAsync(_message));

                _certificateValidationService.Verify(s => s.TrySaveResultAsync(It.IsAny<EndCertificateValidation>(), It.IsAny<CertificateVerificationResult>()), Times.Never);
                _validationEnqueuer.Verify(
                    x => x.StartValidationAsync(It.IsAny<PackageValidationMessageData>()),
                    Times.Never);
            }

            [Fact]
            public async Task FailsIfCertificateIsKnownToBeRevokedAndMessageDoesntHaveOverride()
            {
                // Arrange
                _certificateValidationService
                    .Setup(s => s.FindCertificateValidationAsync(It.IsAny<CertificateValidationMessage>()))
                    .ReturnsAsync(new EndCertificateValidation
                    {
                        Status = null,
                        EndCertificate = new EndCertificate
                        {
                            Status = EndCertificateStatus.Revoked
                        }
                    });

                // Act & Assert
                // The certificate is known to be revoked, the handler should block validation as the message does not have the
                // "RevalidateRevokedCertificate" override set.
                Assert.True(await _target.HandleAsync(_message));

                _certificateValidationService.Verify(s => s.TrySaveResultAsync(It.IsAny<EndCertificateValidation>(), It.IsAny<CertificateVerificationResult>()), Times.Never);
                _validationEnqueuer.Verify(
                    x => x.StartValidationAsync(It.IsAny<PackageValidationMessageData>()),
                    Times.Never);
            }

            [Fact]
            public async Task DownloadsCertificates()
            {
                // Arrange
                var endCertificate = new X509Certificate2();
                var parentCertificate1 = new X509Certificate2();
                var parentCertificate2 = new X509Certificate2();

                _certificateValidationService
                    .Setup(s => s.FindCertificateValidationAsync(It.IsAny<CertificateValidationMessage>()))
                    .ReturnsAsync(new EndCertificateValidation
                    {
                        Status = null,
                        EndCertificate = new EndCertificate
                        {
                            Thumbprint = "End Certificate",
                            Status = EndCertificateStatus.Unknown,
                            Use = EndCertificateUse.CodeSigning,
                            CertificateChainLinks = new CertificateChainLink[]
                            {
                                new CertificateChainLink
                                {
                                    ParentCertificate = new ParentCertificate
                                    {
                                        Thumbprint = "Parent Certificate #1",
                                    }
                                },
                                new CertificateChainLink
                                {
                                    ParentCertificate = new ParentCertificate
                                    {
                                        Thumbprint = "Parent Certificate #2",
                                    }
                                }
                            }
                        }
                    });

                _certificateStore
                    .Setup(s => s.LoadAsync(
                        It.Is<string>(t => t == "End Certificate"),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(endCertificate);

                _certificateStore
                    .Setup(s => s.LoadAsync(
                        It.Is<string>(t => t == "Parent Certificate #1"),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(parentCertificate1);

                _certificateStore
                    .Setup(s => s.LoadAsync(
                        It.Is<string>(t => t == "Parent Certificate #2"),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(parentCertificate2);

                // Act & Arrange
                await _target.HandleAsync(_message);

                // Assert all 3 certificates are loaded.
                _certificateStore.Verify(
                    s => s.LoadAsync(
                        It.Is<string>(t => t == "End Certificate"),
                        It.IsAny<CancellationToken>()),
                    Times.Once);

                _certificateStore.Verify(
                    s => s.LoadAsync(
                        It.Is<string>(t => t == "Parent Certificate #1"),
                        It.IsAny<CancellationToken>()),
                    Times.Once);

                _certificateStore.Verify(
                    s => s.LoadAsync(
                        It.Is<string>(t => t == "Parent Certificate #2"),
                        It.IsAny<CancellationToken>()),
                    Times.Once);

                // Assert that the verifier was given the right certs.
                _certificateVerifier.Verify(
                    v => v.VerifyCodeSigningCertificate(
                        It.Is<X509Certificate2>(c => ReferenceEquals(c, endCertificate)),
                        It.Is<X509Certificate2[]>(e => e.Length == 2 &&
                                                        ReferenceEquals(e[0], parentCertificate1) &&
                                                        ReferenceEquals(e[1], parentCertificate2))),
                    Times.Once);
            }

            [Fact]
            public async Task VerifiesCodesigningCertificateIfCertificateIsCodesigningCertificate()
            {
                // Arrange
                _certificateValidationService
                    .Setup(s => s.FindCertificateValidationAsync(It.IsAny<CertificateValidationMessage>()))
                    .ReturnsAsync(new EndCertificateValidation
                    {
                        Status = null,
                        EndCertificate = new EndCertificate
                        {
                            Status = EndCertificateStatus.Unknown,
                            Use = EndCertificateUse.CodeSigning,
                            CertificateChainLinks = new CertificateChainLink[0]
                        }
                    });

                _certificateStore
                    .Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new X509Certificate2());

                // Act & Arrange
                await _target.HandleAsync(_message);

                // Assert that the the codesigning verifier is called.
                _certificateVerifier.Verify(
                    v => v.VerifyCodeSigningCertificate(
                        It.IsAny<X509Certificate2>(),
                        It.IsAny<X509Certificate2[]>()),
                    Times.Once);
            }

            [Fact]
            public async Task VerifiesTimestampingCertificateIfCertificateIsTimestampingCertificate()
            {
                // Arrange
                _certificateValidationService
                    .Setup(s => s.FindCertificateValidationAsync(It.IsAny<CertificateValidationMessage>()))
                    .ReturnsAsync(new EndCertificateValidation
                    {
                        Status = null,
                        EndCertificate = new EndCertificate
                        {
                            Status = EndCertificateStatus.Unknown,
                            Use = EndCertificateUse.Timestamping,
                            CertificateChainLinks = new CertificateChainLink[0]
                        }
                    });

                _certificateStore
                    .Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new X509Certificate2());

                // Act & Arrange
                await _target.HandleAsync(_message);

                // Assert that the the timestamping verifier is called.
                _certificateVerifier.Verify(
                    v => v.VerifyTimestampingCertificate(
                        It.IsAny<X509Certificate2>(),
                        It.IsAny<X509Certificate2[]>()),
                    Times.Once);
            }

            [Fact]
            public async Task RetriesIfSavingCertificateValidationEntityFails()
            {
                // Arrange
                _certificateValidationService
                    .Setup(s => s.FindCertificateValidationAsync(It.IsAny<CertificateValidationMessage>()))
                    .ReturnsAsync(new EndCertificateValidation
                    {
                        Status = null,
                        EndCertificate = new EndCertificate
                        {
                            Status = EndCertificateStatus.Unknown,
                            Use = EndCertificateUse.CodeSigning,
                            CertificateChainLinks = new CertificateChainLink[0]
                        }
                    });

                _certificateStore
                    .Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new X509Certificate2());

                _certificateValidationService
                    .Setup(s => s.TrySaveResultAsync(It.IsAny<EndCertificateValidation>(), It.IsAny<CertificateVerificationResult>()))
                    .ReturnsAsync(false);

                // Act & Assert
                // Saving the result failed, the handler should retry.
                Assert.False(await _target.HandleAsync(_message));
            }

            public static IEnumerable<object[]> MessageIsConsumedIfValidationEndsGracefullyData()
            {
                yield return new[]
                {
                    new CertificateVerificationResult(
                        status: EndCertificateStatus.Good,
                        statusFlags: X509ChainStatusFlags.NoError)
                };

                yield return new[]
                {
                    new CertificateVerificationResult(
                        status: EndCertificateStatus.Invalid,
                        statusFlags: X509ChainStatusFlags.ExplicitDistrust)
                };

                yield return new[]
                {
                    new CertificateVerificationResult(
                        status: EndCertificateStatus.Revoked,
                        statusFlags: X509ChainStatusFlags.Revoked,
                        revocationTime: DateTime.UtcNow)
                };
            }

            [Theory]
            [MemberData(nameof(MessageIsConsumedIfValidationEndsGracefullyData))]
            public async Task MessageIsConsumedIfValidationEndsGracefully(CertificateVerificationResult result)
            {
                // Arrange
                _certificateValidationService
                    .Setup(s => s.FindCertificateValidationAsync(It.IsAny<CertificateValidationMessage>()))
                    .ReturnsAsync(new EndCertificateValidation
                    {
                        Status = null,
                        EndCertificate = new EndCertificate
                        {
                            Status = EndCertificateStatus.Unknown,
                            Use = EndCertificateUse.CodeSigning,
                            CertificateChainLinks = new CertificateChainLink[0],
                        }
                    });

                _certificateStore
                    .Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new X509Certificate2());

                _certificateVerifier
                    .Setup(v => v.VerifyCodeSigningCertificate(It.IsAny<X509Certificate2>(), It.IsAny<X509Certificate2[]>()))
                    .Returns(result);

                _certificateValidationService
                    .Setup(s => s.TrySaveResultAsync(It.IsAny<EndCertificateValidation>(), It.IsAny<CertificateVerificationResult>()))
                    .ReturnsAsync(true);

                // Act & Assert
                Assert.True(await _target.HandleAsync(_message));

                _certificateValidationService
                    .Verify(s => s.TrySaveResultAsync(It.IsAny<EndCertificateValidation>(), It.IsAny<CertificateVerificationResult>()), Times.Once);
                _validationEnqueuer.Verify(
                    x => x.StartValidationAsync(It.Is<PackageValidationMessageData>(d => d.Type == PackageValidationMessageType.CheckValidator)),
                    Times.Once);
                _validationEnqueuer.Verify(
                    x => x.StartValidationAsync(It.IsAny<PackageValidationMessageData>()),
                    Times.Once);
            }

            [Fact]
            public async Task DoesNotSendCheckValidatorIfToldNotTo()
            {
                // Arrange
                var result = new CertificateVerificationResult(
                    status: EndCertificateStatus.Good,
                    statusFlags: X509ChainStatusFlags.NoError);
                _message = new CertificateValidationMessage(
                    CertificateKey,
                    ValidationId,
                    revalidateRevokedCertificate: false,
                    sendCheckValidator: false);
                _certificateValidationService
                    .Setup(s => s.FindCertificateValidationAsync(It.IsAny<CertificateValidationMessage>()))
                    .ReturnsAsync(new EndCertificateValidation
                    {
                        Status = null,
                        EndCertificate = new EndCertificate
                        {
                            Status = EndCertificateStatus.Unknown,
                            Use = EndCertificateUse.CodeSigning,
                            CertificateChainLinks = new CertificateChainLink[0],
                        }
                    });

                _certificateStore
                    .Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new X509Certificate2());

                _certificateVerifier
                    .Setup(v => v.VerifyCodeSigningCertificate(It.IsAny<X509Certificate2>(), It.IsAny<X509Certificate2[]>()))
                    .Returns(result);

                _certificateValidationService
                    .Setup(s => s.TrySaveResultAsync(It.IsAny<EndCertificateValidation>(), It.IsAny<CertificateVerificationResult>()))
                    .ReturnsAsync(true);

                // Act & Assert
                Assert.True(await _target.HandleAsync(_message));

                _certificateValidationService
                    .Verify(s => s.TrySaveResultAsync(It.IsAny<EndCertificateValidation>(), It.IsAny<CertificateVerificationResult>()), Times.Once);
                _validationEnqueuer.Verify(
                    x => x.StartValidationAsync(It.IsAny<PackageValidationMessageData>()),
                    Times.Never);
            }

            [Fact]
            public async Task DoesNotSendCheckValidatorIfFeatureFlagIsDisabled()
            {
                // Arrange
                var result = new CertificateVerificationResult(
                    status: EndCertificateStatus.Good,
                    statusFlags: X509ChainStatusFlags.NoError);
                _message = new CertificateValidationMessage(
                    CertificateKey,
                    ValidationId,
                    revalidateRevokedCertificate: false,
                    sendCheckValidator: true);
                _certificateValidationService
                    .Setup(s => s.FindCertificateValidationAsync(It.IsAny<CertificateValidationMessage>()))
                    .ReturnsAsync(new EndCertificateValidation
                    {
                        Status = null,
                        EndCertificate = new EndCertificate
                        {
                            Status = EndCertificateStatus.Unknown,
                            Use = EndCertificateUse.CodeSigning,
                            CertificateChainLinks = new CertificateChainLink[0],
                        }
                    });
                _featureFlagService.Setup(x => x.IsQueueBackEnabled()).Returns(false);

                _certificateStore
                    .Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new X509Certificate2());

                _certificateVerifier
                    .Setup(v => v.VerifyCodeSigningCertificate(It.IsAny<X509Certificate2>(), It.IsAny<X509Certificate2[]>()))
                    .Returns(result);

                _certificateValidationService
                    .Setup(s => s.TrySaveResultAsync(It.IsAny<EndCertificateValidation>(), It.IsAny<CertificateVerificationResult>()))
                    .ReturnsAsync(true);

                // Act & Assert
                Assert.True(await _target.HandleAsync(_message));

                _certificateValidationService
                    .Verify(s => s.TrySaveResultAsync(It.IsAny<EndCertificateValidation>(), It.IsAny<CertificateVerificationResult>()), Times.Once);
                _validationEnqueuer.Verify(
                    x => x.StartValidationAsync(It.IsAny<PackageValidationMessageData>()),
                    Times.Never);
            }

            [Fact]
            public async Task RetriesIfValidationFailureIsBelowThreshold()
            {
                // Arrange & Act & Assert
                // The maximum validation failures is set to 5. A certificate whose verification result is "Unknown" with 3
                // pre-existing validation failures should be retried.
                Assert.False(await HandleUnknownResultAsync(validationFailuresStart: 3));
            }

            [Fact]
            public async Task MessageIsConsumedIfValidationFailureCountReachesThreshold()
            {
                // Arrange & Act & Assert
                // The maximum validation failures is set to 5. A certificate whose verification result is "Unknown" with 4
                // pre-existing validation failures should NOT be retried.
                Assert.True(await HandleUnknownResultAsync(validationFailuresStart: 4));
            }

            private async Task<bool> HandleUnknownResultAsync(int validationFailuresStart)
            {
                // Arrange
                // Return an "Unknown" status for the certificate's verification. The validation service should increment the number
                // of failures for the validation's certificate.
                var certificateValidation = new EndCertificateValidation
                {
                    Status = null,
                    EndCertificate = new EndCertificate
                    {
                        Status = EndCertificateStatus.Unknown,
                        Use = EndCertificateUse.CodeSigning,
                        ValidationFailures = validationFailuresStart,
                        CertificateChainLinks = new CertificateChainLink[0],
                    }
                };

                var certificateVerificationResult = new CertificateVerificationResult(
                                                            status: EndCertificateStatus.Unknown,
                                                            statusFlags: X509ChainStatusFlags.RevocationStatusUnknown);

                _certificateValidationService
                    .Setup(s => s.FindCertificateValidationAsync(It.IsAny<CertificateValidationMessage>()))
                    .ReturnsAsync(certificateValidation);

                _certificateStore
                    .Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new X509Certificate2());

                _certificateVerifier
                    .Setup(v => v.VerifyCodeSigningCertificate(It.IsAny<X509Certificate2>(), It.IsAny<X509Certificate2[]>()))
                    .Returns(certificateVerificationResult);

                _certificateValidationService
                    .Setup(
                        s => s.TrySaveResultAsync(
                                It.IsAny<EndCertificateValidation>(),
                                It.Is<CertificateVerificationResult>(r => r.Status == EndCertificateStatus.Unknown)))
                    .Callback<EndCertificateValidation, CertificateVerificationResult>((v, r) => v.EndCertificate.ValidationFailures++)
                    .ReturnsAsync(true);

                // Act & Assert
                var result = await _target.HandleAsync(_message);

                Assert.Equal(validationFailuresStart + 1, certificateValidation.EndCertificate.ValidationFailures);

                _certificateValidationService
                    .Verify(
                        s => s.TrySaveResultAsync(
                                    It.IsAny<EndCertificateValidation>(),
                                    It.IsAny<CertificateVerificationResult>()),
                        Times.Once);

                return result;
            }
        }

        public class FactsBase
        {
            protected readonly Mock<ICertificateStore> _certificateStore;
            protected readonly Mock<ICertificateValidationService> _certificateValidationService;
            protected readonly Mock<ICertificateVerifier> _certificateVerifier;
            protected readonly Mock<IPackageValidationEnqueuer> _validationEnqueuer;
            protected readonly Mock<IFeatureFlagService> _featureFlagService;
            protected CertificateValidationMessage _message;

            protected readonly CertificateValidationMessageHandler _target;

            public FactsBase(int maximumValidationFailures = 5)
            {
                _certificateStore = new Mock<ICertificateStore>();
                _certificateValidationService = new Mock<ICertificateValidationService>();
                _certificateVerifier = new Mock<ICertificateVerifier>();
                _validationEnqueuer = new Mock<IPackageValidationEnqueuer>();
                _featureFlagService = new Mock<IFeatureFlagService>();

                _featureFlagService.SetReturnsDefault(true);

                _message = new CertificateValidationMessage(
                    CertificateKey,
                    ValidationId,
                    revalidateRevokedCertificate: false,
                    sendCheckValidator: true);

                var logger = new Mock<ILogger<CertificateValidationMessageHandler>>();

                _target = new CertificateValidationMessageHandler(
                    _certificateStore.Object,
                    _certificateValidationService.Object,
                    _certificateVerifier.Object,
                    _validationEnqueuer.Object,
                    _featureFlagService.Object,
                    logger.Object,
                    maximumValidationFailures);
            }

        }
    }
}
