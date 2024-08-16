// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Validation;
using Tests.ContextHelpers;
using Xunit;

namespace Validation.PackageSigning.RevalidateCertificate.Tests
{
    public class CertificateRevalidatorFacts
    {
        public class ThePromoteSignaturesAsyncMethod : Base
        {
            [Fact]
            public async Task TracksDuration()
            {
                // Arrange
                var durationMetric = new Mock<IDisposable>();

                _context.Mock();

                _telemetry.Setup(t => t.TrackPromoteSignaturesDuration())
                    .Returns(durationMetric.Object);

                // Act & Assert
                await _target.PromoteSignaturesAsync();

                _telemetry.Verify(t => t.TrackPromoteSignaturesDuration(), Times.Once);
                durationMetric.Verify(m => m.Dispose(), Times.Once);
            }

            [Fact]
            public async Task DoesNoPromotionsIfNonePromotable()
            {
                // Arrange - make signature nonpromotable due to stale end certificate status.
                var signature1 = PromotableSignature;
                var signature2 = PromotableSignature;

                signature1.EndCertificate.StatusUpdateTime = signature1.TrustedTimestamps.First().Value.AddDays(-1);
                signature2.TrustedTimestamps.First().EndCertificate.StatusUpdateTime = signature2.TrustedTimestamps.First().Value.AddDays(-1);

                _context.Mock(packageSignatures: new[] { signature1, signature2 });

                // Act & Assert
                await _target.PromoteSignaturesAsync();

                _context.Verify(c => c.SaveChangesAsync(), Times.Never);

                Assert.Equal(PackageSignatureStatus.InGracePeriod, signature1.Status);
                Assert.Equal(PackageSignatureStatus.InGracePeriod, signature2.Status);
            }

            [Theory]
            [InlineData(PackageSignatureType.Repository)]
            [InlineData((PackageSignatureType)0)]
            public async Task DoesNotPromoteNonAuthorSignatures(PackageSignatureType type)
            {
                // Arrange - make signature nonpromotable due to repository type.
                var signature = PromotableSignature;

                signature.Type = type;

                _context.Mock(packageSignatures: new[] { signature });

                // Act & Assert
                await _target.PromoteSignaturesAsync();

                _context.Verify(c => c.SaveChangesAsync(), Times.Never);

                Assert.Equal(PackageSignatureStatus.InGracePeriod, signature.Status);
            }

            [Fact]
            public async Task PromotesSignaturesIfPossible()
            {
                // Arrange - make signature1 nonpromotable due to stale end certificate status.
                var signature1 = PromotableSignature;
                var signature2 = PromotableSignature;

                signature1.EndCertificate.StatusUpdateTime = signature1.TrustedTimestamps.First().Value.AddDays(-1);

                _context.Mock(packageSignatures: new[] { signature1, signature2 });

                // Act & Assert
                await _target.PromoteSignaturesAsync();

                _context.Verify(c => c.SaveChangesAsync(), Times.Once);

                Assert.Equal(PackageSignatureStatus.InGracePeriod, signature1.Status);
                Assert.Equal(PackageSignatureStatus.Valid, signature2.Status);
            }

            [Fact]
            public async Task PromotesSignatureWithRevokedCertificate()
            {
                var signature = PromotableSignature;

                signature.EndCertificate.Status = EndCertificateStatus.Revoked;
                signature.EndCertificate.StatusUpdateTime = signature.TrustedTimestamps.First().Value.AddDays(-1);

                _context.Mock(packageSignatures: new[] { signature });

                // Act & Assert
                await _target.PromoteSignaturesAsync();

                _context.Verify(c => c.SaveChangesAsync(), Times.Once);

                Assert.Equal(PackageSignatureStatus.Valid, signature.Status);
            }

            [Fact]
            public async Task DoesNotPromoteSignaturesWithNoStatusUpdateTimes()
            {
                // Both timestamp cert and signature cert
                var signature1 = PromotableSignature;
                var signature2 = PromotableSignature;

                signature1.EndCertificate.StatusUpdateTime = null;
                signature2.TrustedTimestamps.First().EndCertificate.StatusUpdateTime = null;

                _context.Mock(packageSignatures: new[] { signature1, signature2 });

                // Act & Assert
                await _target.PromoteSignaturesAsync();

                _context.Verify(c => c.SaveChangesAsync(), Times.Never);

                Assert.Equal(PackageSignatureStatus.InGracePeriod, signature1.Status);
                Assert.Equal(PackageSignatureStatus.InGracePeriod, signature2.Status);
            }

            [Fact]
            public async Task RespectsConfiguredBatchSize()
            {
                // Arrange
                var signature1 = PromotableSignature;
                var signature2 = PromotableSignature;
                var signature3 = PromotableSignature;

                signature1.CreatedAt = DateTime.UtcNow.AddDays(-3);
                signature2.CreatedAt = DateTime.UtcNow.AddDays(-2);
                signature3.CreatedAt = DateTime.UtcNow.AddDays(-1);

                _context.Mock(packageSignatures: new[] { signature1, signature2, signature3 });

                // Act & Assert
                await _target.PromoteSignaturesAsync();

                _context.Verify(c => c.SaveChangesAsync(), Times.Once);

                Assert.Equal(PackageSignatureStatus.Valid, signature1.Status);
                Assert.Equal(PackageSignatureStatus.Valid, signature2.Status);
                Assert.Equal(PackageSignatureStatus.InGracePeriod, signature3.Status);
            }
        }

        public class TheRevalidateStaleCertificatesAsyncMethod : Base
        {
            [Fact]
            public async Task TracksDuration()
            {
                // Arrange
                var durationMetric = new Mock<IDisposable>();

                _context.Mock();

                _telemetry.Setup(t => t.TrackCertificateRevalidationDuration())
                    .Returns(durationMetric.Object);

                // Act & Assert
                await _target.RevalidateStaleCertificatesAsync();

                _telemetry.Verify(t => t.TrackCertificateRevalidationDuration(), Times.Once);
                durationMetric.Verify(m => m.Dispose(), Times.Once);
            }

            [Fact]
            public async Task EnqueuesRevalidationsAndThenSaves()
            {
                // Arrange
                var certificate1 = StaleCertificate;
                var certificate2 = StaleCertificate;

                certificate2.LastVerificationTime = DateTime.UtcNow;

                _context.Mock(endCertificates: new[] { certificate1, certificate2 });

                var enqueueCalled = false;
                var saveCalled = false;
                var valid = true;

                _enqueuer.Setup(e => e.EnqueueValidationAsync(It.IsAny<Guid>(), certificate1))
                    .Callback(() =>
                    {
                        valid = valid ? !saveCalled : false;
                        enqueueCalled = true;
                    })
                    .Returns(Task.CompletedTask);

                _context.Setup(c => c.SaveChangesAsync())
                    .Callback(() =>
                    {
                        valid = valid ? enqueueCalled : false;
                        saveCalled = true;
                    })
                    .Returns(Task.FromResult(0));

                // Act & Assert
                await _target.RevalidateStaleCertificatesAsync();

                _enqueuer.Verify(e => e.EnqueueValidationAsync(It.IsAny<Guid>(), certificate1), Times.Once);
                _enqueuer.Verify(e => e.EnqueueValidationAsync(It.IsAny<Guid>(), certificate2), Times.Never);
                _context.Verify(c => c.SaveChangesAsync(), Times.Once);

                Assert.True(valid);
            }

            [Fact]
            public async Task DoesNotRevalidateFreshCertificates()
            {
                // Arrange
                var certificate = StaleCertificate;

                certificate.LastVerificationTime = DateTime.UtcNow;

                _context.Mock(endCertificates: new[] { certificate });

                // Act & Assert
                await _target.RevalidateStaleCertificatesAsync();

                _enqueuer.Verify(e => e.EnqueueValidationAsync(It.IsAny<Guid>(), certificate), Times.Never);
                _context.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task DoesNotRevalidateRevokedCertificates()
            {
                // Arrange
                var certificate = StaleCertificate;

                certificate.Status = EndCertificateStatus.Revoked;

                _context.Mock(endCertificates: new[] { certificate });

                // Act & Assert
                await _target.RevalidateStaleCertificatesAsync();

                _enqueuer.Verify(e => e.EnqueueValidationAsync(It.IsAny<Guid>(), certificate), Times.Never);
                _context.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task DoesNotRevalidateCertificatesThatHaventFinishedInitialValidation()
            {
                // Arrange
                var certificate = StaleCertificate;

                certificate.Status = EndCertificateStatus.Unknown;
                certificate.LastVerificationTime = null;

                _context.Mock(endCertificates: new[] { certificate });

                // Act & Assert
                await _target.RevalidateStaleCertificatesAsync();

                _enqueuer.Verify(e => e.EnqueueValidationAsync(It.IsAny<Guid>(), certificate), Times.Never);
                _context.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task TracksRevalidationTimeouts()
            {
                // Arrange
                var certificate = StaleCertificate;

                _context.Mock(endCertificates: new[] { certificate });

                Guid validationId = Guid.Empty;

                _enqueuer.Setup(e => e.EnqueueValidationAsync(It.IsAny<Guid>(), certificate))
                    .Callback((Guid v, EndCertificate c) => validationId = v)
                    .Returns(Task.CompletedTask);

                // Add a certificate validation that never finishes, thereby forcing a timeout.
                _context.Setup(c => c.SaveChangesAsync())
                    .Callback(() =>
                    {
                        _context.Mock(certificateValidations: new[]
                        {
                            new EndCertificateValidation
                            {
                                ValidationId = validationId,
                                Status = null,
                            }
                        });
                    })
                    .Returns(Task.FromResult(0));

                // Act & Assert
                await _target.RevalidateStaleCertificatesAsync();

                _enqueuer.Verify(e => e.EnqueueValidationAsync(It.IsAny<Guid>(), certificate), Times.Once);
                _context.Verify(c => c.SaveChangesAsync(), Times.Once);
                _telemetry.Verify(t => t.TrackCertificateRevalidationTakingTooLong(), Times.AtLeastOnce);
                _telemetry.Verify(t => t.TrackCertificateRevalidationReachedTimeout(), Times.Once);
            }

            [Fact]
            public async Task RespectsConfiguredBatchSize()
            {
                // Arrange
                var certificate1 = StaleCertificate;
                var certificate2 = StaleCertificate;
                var certificate3 = StaleCertificate;

                certificate1.LastVerificationTime = certificate3.LastVerificationTime.Value.AddDays(-2);
                certificate2.LastVerificationTime = certificate3.LastVerificationTime.Value.AddDays(-1);

                _context.Mock(endCertificates: new[] { certificate1, certificate2, certificate3 });

                // Act & Assert
                await _target.RevalidateStaleCertificatesAsync();

                _enqueuer.Verify(e => e.EnqueueValidationAsync(It.IsAny<Guid>(), certificate1), Times.Once);
                _enqueuer.Verify(e => e.EnqueueValidationAsync(It.IsAny<Guid>(), certificate2), Times.Once);
                _enqueuer.Verify(e => e.EnqueueValidationAsync(It.IsAny<Guid>(), certificate3), Times.Never);
                _context.Verify(c => c.SaveChangesAsync(), Times.Once);
            }
        }

        public class Base
        {
            protected readonly RevalidationConfiguration _config;
            protected readonly Mock<IValidationEntitiesContext> _context;
            protected readonly Mock<IValidateCertificateEnqueuer> _enqueuer;
            protected readonly Mock<ITelemetryService> _telemetry;

            protected readonly CertificateRevalidator _target;

            public Base()
            {
                _config = new RevalidationConfiguration
                {
                    SignaturePromotionScanSize = 50,
                    SignaturePromotionBatchSize = 2,
                    CertificateRevalidationBatchSize = 2,
                    RevalidationPeriodForCertificates = TimeSpan.FromDays(1),
                    CertificateRevalidationPollTime = TimeSpan.FromSeconds(0.05),
                    CertificateRevalidationTrackAfter = TimeSpan.FromSeconds(0.2),
                    CertificateRevalidationTimeout = TimeSpan.FromSeconds(0.3),
                };

                _context = new Mock<IValidationEntitiesContext>();
                _enqueuer = new Mock<IValidateCertificateEnqueuer>();
                _telemetry = new Mock<ITelemetryService>();

                _target = new CertificateRevalidator(
                    _config,
                    _context.Object,
                    _enqueuer.Object,
                    _telemetry.Object,
                    Mock.Of<ILogger<CertificateRevalidator>>());
            }

            protected PackageSignature PromotableSignature =>
                new PackageSignature
                {
                    Status = PackageSignatureStatus.InGracePeriod,

                    EndCertificate = new EndCertificate
                    {
                        Status = EndCertificateStatus.Good,
                        StatusUpdateTime = DateTime.UtcNow,
                    },

                    TrustedTimestamps = new TrustedTimestamp[]
                    {
                        new TrustedTimestamp
                        {
                            Status = TrustedTimestampStatus.Valid,
                            Value = DateTime.UtcNow.AddYears(-1),

                            EndCertificate = new EndCertificate
                            {
                                Status = EndCertificateStatus.Good,
                                StatusUpdateTime = DateTime.UtcNow,
                            }
                        }
                    },

                    Type = PackageSignatureType.Author,
                };

            protected EndCertificate StaleCertificate =>
                new EndCertificate
                {
                    Status = EndCertificateStatus.Good,
                    LastVerificationTime = DateTime.UtcNow
                        .Subtract(_config.RevalidationPeriodForCertificates)
                        .AddDays(-1),
                    Validations = new[]
                    {
                        new EndCertificateValidation
                        {
                            Status = EndCertificateStatus.Good
                        }
                    }
                };
        }
    }
}
