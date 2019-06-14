// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGet.Services.Validation.PackageSigning.ValidateCertificate;
using Tests.ContextHelpers;
using Xunit;

namespace NuGet.Services.Validation.PackageSigning
{
    public class PackageCertificatesValidatorFacts
    {
        private const int PackageKey = 1001;
        private const string PackageId = "NuGet.Versioning";
        private const string PackageNormalizedVersion = "4.3.0.0-ALPHA+git";
        private static readonly Guid ValidationId = new Guid("fb9c0bac-3d4d-4cc7-ac2d-b3940e15b94d");
        private static readonly Guid OtherValidationId = new Guid("6593BD33-ABC0-4049-BDCF-915807F1D2B3");
        private const string NupkgUrl = "https://nuget.test/nuget.versioning/4.3.0/package.nupkg";

        public class TheGetStatusMethod : FactsBase
        {
            public static IEnumerable<object[]> ReturnsPersistedStatusIfNotIncompleteData()
            {
                return Enum.GetValues(typeof(ValidationStatus))
                           .Cast<ValidationStatus>()
                           .Where(v => v != ValidationStatus.Incomplete)
                           .Select(s => new object[] { s });
            }

            [Fact]
            public async Task ReturnsValidatorIssues()
            {
                // Arrange
                _validationContext.Mock(validatorStatuses: new[]
                {
                    new ValidatorStatus
                    {
                        ValidationId = ValidationId,
                        PackageKey = PackageKey,
                        ValidatorName = nameof(PackageCertificatesValidator),
                        State = ValidationStatus.Failed,
                        ValidatorIssues = new List<ValidatorIssue>
                        {
                            new ValidatorIssue
                            {
                                IssueCode = (ValidationIssueCode)987,
                                Data = "{}",
                            },
                            new ValidatorIssue
                            {
                                IssueCode = ValidationIssueCode.ClientSigningVerificationFailure,
                                Data = "unknown contract",
                            },
                        },
                    }
                });

                // Act
                var actual = await _target.GetResultAsync(_validationRequest.Object);

                // Assert
                Assert.Equal(ValidationStatus.Failed, actual.Status);
                Assert.Equal(2, actual.Issues.Count);

                Assert.Equal((ValidationIssueCode)987, actual.Issues[0].IssueCode);
                Assert.Equal("{}", actual.Issues[0].Serialize());

                Assert.Equal(ValidationIssueCode.ClientSigningVerificationFailure, actual.Issues[1].IssueCode);
                Assert.Equal("unknown contract", actual.Issues[1].Serialize());
            }

            [Theory]
            [MemberData(nameof(ReturnsPersistedStatusIfNotIncompleteData))]
            public async Task ReturnsPersistedStatusIfNotIncomplete(ValidationStatus status)
            {
                // Arrange
                _validationContext.Mock(validatorStatuses: new[]
                {
                    new ValidatorStatus
                    {
                        ValidationId = ValidationId,
                        PackageKey = PackageKey,
                        ValidatorName = nameof(PackageCertificatesValidator),
                        State = status,
                        ValidatorIssues = new List<ValidatorIssue>(),
                    }
                });

                // Act & Assert
                var actual = await _target.GetResultAsync(_validationRequest.Object);

                Assert.Equal(status, actual.Status);
            }

            public static IEnumerable<object[]> GetReturnsExpectedStatusForCertificateValidationsData()
            {
                // If one or more certificate validation isn't done (has a status of "null"), the overall validation
                // should be "Incomplete". Note that a validation with a "Revoked" certificate should eventually fail
                // once all certificate validations have completed.
                yield return new object[]
                {
                    ValidationStatus.Incomplete,
                    null,
                };

                // If ALL certificate validations have a status of "Good" or "Unknown", the overall validation
                // should be "Succeeded".
                yield return new object[]
                {
                    ValidationStatus.Succeeded,
                    EndCertificateStatus.Good,
                };

                yield return new object[]
                {
                    ValidationStatus.Succeeded,
                    EndCertificateStatus.Unknown,
                };
            }

            [Theory]
            [MemberData(nameof(GetReturnsExpectedStatusForCertificateValidationsData))]
            public async Task ReturnsExpectedStatusForCertificateValidations(ValidationStatus expectedStatus, EndCertificateStatus? certificateStatus)
            {
                // Arrange
                var certificates = new List<EndCertificate>();
                var certificateValidations = new List<EndCertificateValidation>();
                var packageSignatures = new List<PackageSignature>();

                var timestamp = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow,
                    EndCertificate = new EndCertificate()
                };

                var certificate = new EndCertificate
                {
                    Status = certificateStatus ?? EndCertificateStatus.Unknown,
                    RevocationTime = DateTime.MaxValue,
                };

                var certificateValidation = new EndCertificateValidation
                {
                    ValidationId = ValidationId,
                    Status = certificateStatus,
                    EndCertificate = certificate
                };

                var packageSignature = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Unknown,
                    EndCertificate = certificate,
                    Type = PackageSignatureType.Author,
                };

                certificate.PackageSignatures = new[] { packageSignature };
                certificate.Validations = new[] { certificateValidation };

                packageSignature.TrustedTimestamps = new[] { timestamp };

                certificates.Add(certificate);
                certificateValidations.Add(certificateValidation);
                packageSignatures.Add(packageSignature);

                _validationContext.Mock(
                    validatorStatuses: new ValidatorStatus[]
                    {
                        new ValidatorStatus
                        {
                            ValidationId = ValidationId,
                            ValidatorName = nameof(PackageCertificatesValidator),
                            PackageKey = PackageKey,
                            State = ValidationStatus.Incomplete,
                            ValidatorIssues = new List<ValidatorIssue>(),
                        }
                    },
                    packageSigningStates: new PackageSigningState[]
                    {
                        new PackageSigningState
                        {
                            PackageKey = PackageKey,
                            PackageId = PackageId,
                            PackageNormalizedVersion = PackageNormalizedVersion,
                            SigningStatus = PackageSigningStatus.Valid,
                            PackageSignatures = packageSignatures,
                        }
                    },
                    packageSignatures: packageSignatures,
                    endCertificates: certificates,
                    certificateValidations: certificateValidations);

                // Act & Assert
                var result = await _target.GetResultAsync(_validationRequest.Object);

                Assert.Equal(expectedStatus, result.Status);
            }

            public static IEnumerable<object[]> InvalidSignatureFailsValidationData()
            {
                yield return new object[]
                {
                    ValidationStatus.Succeeded, PackageSignatureStatus.Unknown
                };

                // Signatures may have "Valid" and "InGracePeriod" Statuses before
                // the CertificateValidator finishes due to revalidations.
                yield return new object[]
                {
                    ValidationStatus.Succeeded, PackageSignatureStatus.Valid
                };

                yield return new object[]
                {
                    ValidationStatus.Succeeded, PackageSignatureStatus.InGracePeriod
                };

                yield return new object[]
                {
                    ValidationStatus.Failed, PackageSignatureStatus.Invalid
                };
            }

            [Theory]
            [MemberData(nameof(InvalidSignatureFailsValidationData))]
            public async Task InvalidSignatureFailsValidation(
                ValidationStatus expectedStatus,
                PackageSignatureStatus packageSignatureStatus)
            {
                var validatorStatus = new ValidatorStatus
                {
                    ValidationId = ValidationId,
                    ValidatorName = nameof(PackageCertificatesValidator),
                    PackageKey = PackageKey,
                    State = ValidationStatus.Incomplete,
                    ValidatorIssues = new List<ValidatorIssue>(),
                };

                var packageSigningState = new PackageSigningState
                {
                    PackageKey = PackageKey,
                    PackageId = PackageId,
                    PackageNormalizedVersion = PackageNormalizedVersion,
                    SigningStatus = PackageSigningStatus.Valid,
                    PackageSignatures = new List<PackageSignature>(),
                };

                var certificate = new EndCertificate
                {
                    Status = EndCertificateStatus.Unknown,
                    PackageSignatures = new List<PackageSignature>(),
                };

                var certificateValidation = new EndCertificateValidation
                {
                    ValidationId = ValidationId,
                    Status = EndCertificateStatus.Unknown,
                };

                var signature = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = packageSignatureStatus,
                    PackageSigningState = packageSigningState,
                    EndCertificate = certificate,
                    Type = PackageSignatureType.Author,
                };

                var timestamp = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow,
                    EndCertificate = certificate,
                };

                packageSigningState.PackageSignatures.Add(signature);
                signature.TrustedTimestamps = new[] { timestamp };
                certificate.PackageSignatures.Add(signature);
                certificate.Validations = new[] { certificateValidation };

                _validationContext.Mock(
                    validatorStatuses: new[] { validatorStatus },
                    packageSigningStates: new[] { packageSigningState },
                    packageSignatures: certificate.PackageSignatures,
                    trustedTimestamps: new[] { timestamp },
                    endCertificates: new[] { certificate },
                    certificateValidations: new[] { certificateValidation });

                // Act & Assert
                var result = await _target.GetResultAsync(_validationRequest.Object);

                Assert.Equal(expectedStatus, result.Status);
            }

            public static IEnumerable<object[]> ValidSignaturesArePromotedData()
            {
                var cert1SecondAgo = new EndCertificate
                {
                    Status = EndCertificateStatus.Good,
                    StatusUpdateTime = DateTime.UtcNow.AddSeconds(-1),
                };

                var certRevoked1SecondAgo = new EndCertificate
                {
                    Status = EndCertificateStatus.Revoked,
                    StatusUpdateTime = DateTime.UtcNow.AddSeconds(-1),
                    RevocationTime = DateTime.UtcNow.AddSeconds(-1),
                };

                var cert1YearAgo = new EndCertificate
                {
                    Status = EndCertificateStatus.Good,
                    StatusUpdateTime = DateTime.UtcNow.AddYears(-1),
                };

                // A signature whose timestamp is BEFORE the signature's and timestamps' certificates
                // last updates should be promoted to "Valid".
                yield return new object[]
                {
                    PackageSignatureStatus.Valid,
                    new PackageSignature
                    {
                        EndCertificate = cert1SecondAgo,
                        TrustedTimestamps = new[]
                        {
                            new TrustedTimestamp
                            {
                                Value = DateTime.UtcNow.AddDays(-1),
                                EndCertificate = cert1SecondAgo,
                            }
                        },
                        Type = PackageSignatureType.Author,
                    },
                };

                // A signature whose timestamp is AFTER the signature's certificate last update should be
                // promoted to "InGracePeriod"
                yield return new object[]
                {
                    PackageSignatureStatus.InGracePeriod,
                    new PackageSignature
                    {
                        EndCertificate = cert1YearAgo,
                        TrustedTimestamps = new[]
                        {
                            new TrustedTimestamp
                            {
                                Value = DateTime.UtcNow.AddDays(-1),
                                EndCertificate = cert1SecondAgo,
                            }
                        },
                        Type = PackageSignatureType.Author,
                    },
                };

                // A signature whose timestamp is AFTER the timestamp's certificate last update should be
                // promoted to "InGracePeriod"
                yield return new object[]
                {
                    PackageSignatureStatus.InGracePeriod,
                    new PackageSignature
                    {
                        EndCertificate = cert1SecondAgo,
                        TrustedTimestamps = new[]
                        {
                            new TrustedTimestamp
                            {
                                Value = DateTime.UtcNow.AddDays(-1),
                                EndCertificate = cert1YearAgo,
                            }
                        },
                        Type = PackageSignatureType.Author,
                    },
                };

                // The latest timestamp should be used for promotion decisions.
                yield return new object[]
                {
                    PackageSignatureStatus.InGracePeriod,
                    new PackageSignature
                    {
                        EndCertificate = cert1YearAgo,
                        TrustedTimestamps = new[]
                        {
                            new TrustedTimestamp
                            {
                                Value = DateTime.UtcNow.AddDays(-1),
                                EndCertificate = cert1YearAgo,
                            },
                            new TrustedTimestamp
                            {
                                Value = DateTime.UtcNow.AddYears(-10),
                                EndCertificate = cert1YearAgo,
                            }
                        },
                        Type = PackageSignatureType.Author,
                    },
                };

                // A signature whose signing certificate is revoked should be promoted to "Valid" as long as the revocation
                // time begins after the package was signed.
                yield return new object[]
                {
                    PackageSignatureStatus.Valid,
                    new PackageSignature
                    {
                        EndCertificate = certRevoked1SecondAgo,
                        TrustedTimestamps = new[]
                        {
                            new TrustedTimestamp
                            {
                                Value = DateTime.UtcNow.AddDays(-1),
                                EndCertificate = cert1SecondAgo,
                            }
                        },
                        Type = PackageSignatureType.Author,
                    },
                };
            }

            [Theory]
            [MemberData(nameof(ValidSignaturesArePromotedData))]
            public async Task ValidSignaturesArePromoted(PackageSignatureStatus expectedStatus, PackageSignature signature)
            {
                var validatorStatus = new ValidatorStatus
                {
                    ValidationId = ValidationId,
                    ValidatorName = nameof(PackageCertificatesValidator),
                    PackageKey = PackageKey,
                    State = ValidationStatus.Incomplete,
                    ValidatorIssues = new List<ValidatorIssue>(),
                };

                var packageSigningState = new PackageSigningState
                {
                    PackageKey = PackageKey,
                    PackageId = PackageId,
                    PackageNormalizedVersion = PackageNormalizedVersion,
                    SigningStatus = PackageSigningStatus.Valid,
                    PackageSignatures = new[] { signature },
                };

                var certificateValidation = new EndCertificateValidation
                {
                    ValidationId = ValidationId,
                    Status = signature.EndCertificate.Status,
                };

                signature.PackageKey = PackageKey;
                signature.Status = PackageSignatureStatus.Unknown;
                signature.EndCertificate.Validations = new[] { certificateValidation };

                _validationContext.Mock(
                    validatorStatuses: new[] { validatorStatus },
                    packageSigningStates: new[] { packageSigningState },
                    packageSignatures: new[] { signature },
                    endCertificates: new[] { signature.EndCertificate },
                    certificateValidations: new[] { certificateValidation });

                // Act & Assert
                var result = await _target.GetResultAsync(_validationRequest.Object);

                Assert.Equal(ValidationStatus.Succeeded, result.Status);

                Assert.Equal(expectedStatus, signature.Status);
            }

            [Fact]
            public async Task ThrowsIfValidSignaturesHasTimestampWithRevokedCertificate()
            {
                var validatorStatus = new ValidatorStatus
                {
                    ValidationId = ValidationId,
                    ValidatorName = nameof(PackageCertificatesValidator),
                    PackageKey = PackageKey,
                    State = ValidationStatus.Incomplete,
                    ValidatorIssues = new List<ValidatorIssue>(),
                };

                var packageSigningState = new PackageSigningState
                {
                    PackageKey = PackageKey,
                    PackageId = PackageId,
                    PackageNormalizedVersion = PackageNormalizedVersion,
                    SigningStatus = PackageSigningStatus.Valid
                };

                var signature = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Unknown,
                    Type = PackageSignatureType.Author,
                };

                var timestamp = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-1)
                };

                var signingCertificate = new EndCertificate
                {
                    Status = EndCertificateStatus.Good,
                    StatusUpdateTime = DateTime.UtcNow.AddSeconds(-1),
                };

                var timestampCertificate = new EndCertificate
                {
                    Status = EndCertificateStatus.Revoked,
                    StatusUpdateTime = DateTime.UtcNow.AddSeconds(-1),
                    RevocationTime = DateTime.UtcNow.AddSeconds(-1),
                };

                packageSigningState.PackageSignatures = new[] { signature };
                signature.PackageSigningState = packageSigningState;
                signature.EndCertificate = signingCertificate;
                signature.TrustedTimestamps = new[] { timestamp };
                timestamp.PackageSignature = signature;
                timestamp.EndCertificate = timestampCertificate;

                _validationContext.Mock(
                    validatorStatuses: new[] { validatorStatus },
                    packageSigningStates: new[] { packageSigningState },
                    packageSignatures: new[] { signature },
                    endCertificates: new[] { signature.EndCertificate, timestampCertificate });

                // Act & Assert
                var ex = await Assert.ThrowsAsync<ArgumentException>(() => _target.GetResultAsync(_validationRequest.Object));

                Assert.Equal($"Package signature {signature.Key} is valid but has a timestamp whose end certificate is revoked\r\nParameter name: signature", ex.Message);
            }

            [Fact]
            public async Task ThrowsIfPackageIsOnlyRepositorySigned()
            {
                var validatorStatus = new ValidatorStatus
                {
                    ValidationId = ValidationId,
                    ValidatorName = nameof(PackageCertificatesValidator),
                    PackageKey = PackageKey,
                    State = ValidationStatus.Incomplete,
                    ValidatorIssues = new List<ValidatorIssue>(),
                };

                var packageSigningState = new PackageSigningState
                {
                    PackageKey = PackageKey,
                    PackageId = PackageId,
                    PackageNormalizedVersion = PackageNormalizedVersion,
                    SigningStatus = PackageSigningStatus.Valid
                };

                var signature = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Unknown,
                    Type = PackageSignatureType.Repository,
                };

                var timestamp = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-1)
                };

                var signingCertificate = new EndCertificate
                {
                    Status = EndCertificateStatus.Good,
                    StatusUpdateTime = DateTime.UtcNow.AddSeconds(-1),
                };

                var timestampCertificate = new EndCertificate
                {
                    Status = EndCertificateStatus.Revoked,
                    StatusUpdateTime = DateTime.UtcNow.AddSeconds(-1),
                    RevocationTime = DateTime.UtcNow.AddSeconds(-1),
                };

                packageSigningState.PackageSignatures = new[] { signature };
                signature.PackageSigningState = packageSigningState;
                signature.EndCertificate = signingCertificate;
                signature.TrustedTimestamps = new[] { timestamp };
                timestamp.PackageSignature = signature;
                timestamp.EndCertificate = timestampCertificate;

                _validationContext.Mock(
                    validatorStatuses: new[] { validatorStatus },
                    packageSigningStates: new[] { packageSigningState },
                    packageSignatures: new[] { signature },
                    endCertificates: new[] { signature.EndCertificate, timestampCertificate });

                // Act & Assert
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _target.GetResultAsync(_validationRequest.Object));

                Assert.Equal($"Package with key {validatorStatus.PackageKey} does not have an author signature", ex.Message);
            }
        }

        public class TheStartValidationAsyncMethod : FactsBase
        {
            private static readonly ValidationStatus[] validationStatusesThatAreStarted = new ValidationStatus[]
            {
                ValidationStatus.Incomplete,
                ValidationStatus.Failed,
                ValidationStatus.Succeeded,
            };

            [Theory]
            [MemberData(nameof(ValidationStatusesThatAreStarted))]
            public async Task ReturnsPersistedStatusIfAlreadyStarted(ValidationStatus status)
            {
                // Arrange
                _validationContext.Mock(validatorStatuses: new[]
                {
                    new ValidatorStatus
                    {
                        ValidationId = ValidationId,
                        PackageKey = PackageKey,
                        ValidatorName = nameof(PackageCertificatesValidator),
                        State = status,
                            ValidatorIssues = new List<ValidatorIssue>(),
                    }
                });

                // Act & Assert
                var actual = await _target.StartAsync(_validationRequest.Object);

                _certificateVerifier.Verify(v => v.EnqueueVerificationAsync(It.IsAny<IValidationRequest>(), It.IsAny<EndCertificate>()), Times.Never);
                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Never);
                _telemetryService.Verify(
                    x => x.TrackDurationToStartPackageCertificatesValidator(_validationRequest.Object.PackageId, _validationRequest.Object.PackageVersion),
                    Times.Never);

                Assert.Equal(status, actual.Status);
            }

            [Fact]
            public async Task UnsignedPackagesAlwaysSucceed()
            {
                var validatorStatus = new ValidatorStatus
                {
                    ValidationId = ValidationId,
                    PackageKey = PackageKey,
                    ValidatorName = nameof(PackageCertificatesValidator),
                    State = ValidationStatus.NotStarted,
                    ValidatorIssues = new List<ValidatorIssue>(),
                };

                var packageSigningState = new PackageSigningState
                {
                    PackageKey = PackageKey,
                    PackageId = PackageId,
                    PackageNormalizedVersion = PackageNormalizedVersion,
                    SigningStatus = PackageSigningStatus.Unsigned
                };

                // Arrange
                _validationContext.Mock(
                    validatorStatuses: new[] { validatorStatus },
                    packageSigningStates: new[] { packageSigningState });

                // Act & Assert
                var actual = await _target.StartAsync(_validationRequest.Object);

                _certificateVerifier.Verify(v => v.EnqueueVerificationAsync(It.IsAny<IValidationRequest>(), It.IsAny<EndCertificate>()), Times.Never);
                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);
                _telemetryService.Verify(
                    x => x.TrackDurationToStartPackageCertificatesValidator(_validationRequest.Object.PackageId, _validationRequest.Object.PackageVersion),
                    Times.Never);

                Assert.Equal(ValidationStatus.Succeeded, actual.Status);
                Assert.Equal(ValidationStatus.Succeeded, validatorStatus.State);
            }

            [Fact]
            public async Task OnlyRepositorySignedPackagesAlwaysSucceed()
            {
                var validatorStatus = new ValidatorStatus
                {
                    ValidationId = ValidationId,
                    PackageKey = PackageKey,
                    ValidatorName = nameof(PackageCertificatesValidator),
                    State = ValidationStatus.NotStarted,
                    ValidatorIssues = new List<ValidatorIssue>(),
                };

                var packageSigningState = new PackageSigningState
                {
                    PackageKey = PackageKey,
                    PackageId = PackageId,
                    PackageNormalizedVersion = PackageNormalizedVersion,
                    SigningStatus = PackageSigningStatus.Valid
                };

                var packageSignature = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Valid,
                    Type = PackageSignatureType.Repository,
                };

                // Arrange
                _validationContext.Mock(
                    validatorStatuses: new[] { validatorStatus },
                    packageSigningStates: new[] { packageSigningState },
                    packageSignatures: new[] { packageSignature });

                // Act & Assert
                var actual = await _target.StartAsync(_validationRequest.Object);

                _certificateVerifier.Verify(v => v.EnqueueVerificationAsync(It.IsAny<IValidationRequest>(), It.IsAny<EndCertificate>()), Times.Never);
                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);
                _telemetryService.Verify(
                    x => x.TrackDurationToStartPackageCertificatesValidator(_validationRequest.Object.PackageId, _validationRequest.Object.PackageVersion),
                    Times.Never);

                Assert.Equal(ValidationStatus.Succeeded, actual.Status);
                Assert.Equal(ValidationStatus.Succeeded, validatorStatus.State);
            }

            [Fact]
            public async Task ReturnsSucceededIfAllCertificatesAlreadyValidated()
            {
                // Arrange
                var validatorStatus = new ValidatorStatus
                {
                    ValidationId = ValidationId,
                    ValidatorName = nameof(PackageCertificatesValidator),
                    PackageKey = PackageKey,
                    State = ValidationStatus.NotStarted,
                    ValidatorIssues = new List<ValidatorIssue>(),
                };

                var packageSigningState = new PackageSigningState
                {
                    PackageKey = PackageKey,
                    PackageId = PackageId,
                    PackageNormalizedVersion = PackageNormalizedVersion,
                    SigningStatus = PackageSigningStatus.Valid,
                };

                var packageSignature = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Valid,
                    Type = PackageSignatureType.Author,
                };

                var timestamp = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-10)
                };

                var signatureCertificate = new EndCertificate
                {
                    Key = 123,
                    Status = EndCertificateStatus.Good,
                    StatusUpdateTime = DateTime.UtcNow.AddSeconds(-10),
                    NextStatusUpdateTime = DateTime.UtcNow.AddDays(1),
                    LastVerificationTime = DateTime.UtcNow.AddSeconds(-10),
                    RevocationTime = null,
                    ValidationFailures = 0,
                };

                var timestampCertificate = new EndCertificate
                {
                    Key = 456,
                    Status = EndCertificateStatus.Good,
                    StatusUpdateTime = DateTime.UtcNow.AddSeconds(-10),
                    NextStatusUpdateTime = DateTime.UtcNow.AddDays(1),
                    LastVerificationTime = DateTime.UtcNow.AddSeconds(-10),
                    RevocationTime = null,
                    ValidationFailures = 0,
                };

                packageSigningState.PackageSignatures = new[] { packageSignature };
                packageSignature.PackageSigningState = packageSigningState;
                packageSignature.TrustedTimestamps = new[] { timestamp };
                packageSignature.EndCertificate = signatureCertificate;
                timestamp.EndCertificate = timestampCertificate;
                signatureCertificate.PackageSignatures = new[] { packageSignature };
                timestampCertificate.TrustedTimestamps = new[] { timestamp };

                _validationContext.Mock(
                    validatorStatuses: new[] { validatorStatus },
                    packageSigningStates: new[] { packageSigningState },
                    packageSignatures: new[] { packageSignature },
                    trustedTimestamps: new[] { timestamp },
                    endCertificates: new[] { signatureCertificate, timestampCertificate });

                // Act & Assert
                var actual = await _target.StartAsync(_validationRequest.Object);

                _certificateVerifier.Verify(v => v.EnqueueVerificationAsync(It.IsAny<IValidationRequest>(), It.IsAny<EndCertificate>()), Times.Never);
                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);
                _telemetryService.Verify(
                    x => x.TrackDurationToStartPackageCertificatesValidator(_validationRequest.Object.PackageId, _validationRequest.Object.PackageVersion),
                    Times.Never);

                Assert.Equal(ValidationStatus.Succeeded, actual.Status);
                Assert.Equal(ValidationStatus.Succeeded, validatorStatus.State);
            }

            [Fact]
            public async Task ReturnsIncompleteIfThereAreCertificatesToValidate()
            {
                // Arrange
                var validatorStatus = new ValidatorStatus
                {
                    ValidationId = ValidationId,
                    ValidatorName = nameof(PackageCertificatesValidator),
                    PackageKey = PackageKey,
                    State = ValidationStatus.NotStarted,
                    ValidatorIssues = new List<ValidatorIssue>(),
                };

                var packageSigningState = new PackageSigningState
                {
                    PackageKey = PackageKey,
                    PackageId = PackageId,
                    PackageNormalizedVersion = PackageNormalizedVersion,
                    SigningStatus = PackageSigningStatus.Valid,
                };

                var packageSignature = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Valid,
                    Type = PackageSignatureType.Author,
                };

                var timestamp = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-10),
                };

                var signatureCertificate = new EndCertificate
                {
                    Key = 123,
                    Status = EndCertificateStatus.Good,
                    StatusUpdateTime = null,
                    NextStatusUpdateTime = null,
                    LastVerificationTime = null,
                    RevocationTime = null,
                    ValidationFailures = 0,
                };

                var timestampCertificate = new EndCertificate
                {
                    Key = 123,
                    Status = EndCertificateStatus.Good,
                    StatusUpdateTime = null,
                    NextStatusUpdateTime = null,
                    LastVerificationTime = null,
                    RevocationTime = null,
                    ValidationFailures = 0,
                };

                packageSigningState.PackageSignatures = new[] { packageSignature };
                packageSignature.PackageSigningState = packageSigningState;
                packageSignature.TrustedTimestamps = new[] { timestamp };
                packageSignature.EndCertificate = signatureCertificate;
                timestamp.EndCertificate = timestampCertificate;
                signatureCertificate.PackageSignatures = new[] { packageSignature };
                timestampCertificate.TrustedTimestamps = new[] { timestamp };

                _validationContext.Mock(
                    validatorStatuses: new[] { validatorStatus },
                    packageSigningStates: new[] { packageSigningState },
                    packageSignatures: new[] { packageSignature },
                    endCertificates: new[] { signatureCertificate, timestampCertificate });

                var actual = await _target.StartAsync(_validationRequest.Object);

                _certificateVerifier.Verify(v => v.EnqueueVerificationAsync(It.IsAny<IValidationRequest>(), It.IsAny<EndCertificate>()), Times.Exactly(2));
                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);
                _telemetryService.Verify(
                    x => x.TrackDurationToStartPackageCertificatesValidator(_validationRequest.Object.PackageId, _validationRequest.Object.PackageVersion),
                    Times.Once);

                Assert.Equal(ValidationStatus.Incomplete, actual.Status);
                Assert.Equal(ValidationStatus.Incomplete, validatorStatus.State);
            }

            [Fact]
            public async Task CertificateRevokedAfterPackageWasSignedDoesntInvalidateSignature()
            {
                // Arrange
                var validatorStatus = new ValidatorStatus
                {
                    ValidationId = ValidationId,
                    ValidatorName = nameof(PackageCertificatesValidator),
                    PackageKey = PackageKey,
                    State = ValidationStatus.NotStarted,
                    ValidatorIssues = new List<ValidatorIssue>(),
                };

                var packageSigningState = new PackageSigningState
                {
                    PackageKey = PackageKey,
                    PackageId = PackageId,
                    PackageNormalizedVersion = PackageNormalizedVersion,
                    SigningStatus = PackageSigningStatus.Valid,
                };

                var packageSignature = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Valid,
                    Type = PackageSignatureType.Author,
                };

                var timestamp = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-10),
                };

                var signatureCertificate = new EndCertificate
                {
                    Key = 123,
                    Use = EndCertificateUse.CodeSigning,
                    Status = EndCertificateStatus.Revoked,
                    StatusUpdateTime = DateTime.UtcNow.AddDays(-20),
                    NextStatusUpdateTime = DateTime.UtcNow.AddDays(1),
                    LastVerificationTime = DateTime.UtcNow.AddDays(-20),
                    RevocationTime = DateTime.UtcNow.AddDays(-1),
                    ValidationFailures = 0,
                };

                var timestampCertificate = new EndCertificate
                {
                    Key = 456,
                    Use = EndCertificateUse.Timestamping,
                    Status = EndCertificateStatus.Unknown,
                    StatusUpdateTime = null,
                    NextStatusUpdateTime = null,
                    LastVerificationTime = null,
                    RevocationTime = null,
                    ValidationFailures = 0,
                };

                packageSigningState.PackageSignatures = new[] { packageSignature };
                packageSignature.PackageSigningState = packageSigningState;
                packageSignature.EndCertificate = signatureCertificate;
                packageSignature.TrustedTimestamps = new[] { timestamp };
                timestamp.EndCertificate = timestampCertificate;
                signatureCertificate.PackageSignatures = new[] { packageSignature };
                timestampCertificate.TrustedTimestamps = new[] { timestamp };

                _validationContext.Mock(
                    validatorStatuses: new[] { validatorStatus },
                    packageSigningStates: new[] { packageSigningState },
                    packageSignatures: new[] { packageSignature },
                    endCertificates: new[] { signatureCertificate, timestampCertificate });

                // Act & Assert (NOTE: the "Revoked" certificate must NOT be verified!)
                var actual = await _target.StartAsync(_validationRequest.Object);

                _certificateVerifier.Verify(v => v.EnqueueVerificationAsync(It.IsAny<IValidationRequest>(), It.IsAny<EndCertificate>()), Times.Once);
                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);
                _telemetryService.Verify(
                    x => x.TrackDurationToStartPackageCertificatesValidator(_validationRequest.Object.PackageId, _validationRequest.Object.PackageVersion),
                    Times.Once);

                Assert.Equal(ValidationStatus.Incomplete, actual.Status);
                Assert.Equal(ValidationStatus.Incomplete, validatorStatus.State);
            }

            [Fact]
            public async Task InvalidCertificatesAlwaysInvalidateSignature()
            {
                // Arrange
                var validatorStatus = new ValidatorStatus
                {
                    ValidationId = ValidationId,
                    ValidatorName = nameof(PackageCertificatesValidator),
                    PackageKey = PackageKey,
                    State = ValidationStatus.NotStarted,
                    ValidatorIssues = new List<ValidatorIssue>(),
                };

                var packageSigningState = new PackageSigningState
                {
                    PackageKey = PackageKey,
                    PackageId = PackageId,
                    PackageNormalizedVersion = PackageNormalizedVersion,
                    SigningStatus = PackageSigningStatus.Valid,
                };

                var packageSignature = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Valid,
                    Type = PackageSignatureType.Author,
                };

                var timestamp = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-10),
                };

                var signatureCertificate = new EndCertificate
                {
                    Key = 123,
                    Use = EndCertificateUse.CodeSigning,
                    Status = EndCertificateStatus.Invalid,
                    StatusUpdateTime = DateTime.UtcNow.AddDays(-20),
                    NextStatusUpdateTime = DateTime.UtcNow.AddDays(1),
                    LastVerificationTime = DateTime.UtcNow.AddDays(-20),
                    RevocationTime = null,
                    ValidationFailures = 0,
                };

                var timestampCertificate = new EndCertificate
                {
                    Key = 456,
                    Status = EndCertificateStatus.Invalid,
                    StatusUpdateTime = DateTime.UtcNow.AddDays(-20),
                    NextStatusUpdateTime = DateTime.UtcNow.AddDays(1),
                    LastVerificationTime = DateTime.UtcNow.AddDays(-20),
                    RevocationTime = null,
                    ValidationFailures = 0,
                };

                packageSigningState.PackageSignatures = new[] { packageSignature };
                packageSignature.PackageSigningState = packageSigningState;
                packageSignature.TrustedTimestamps = new[] { timestamp };
                packageSignature.EndCertificate = signatureCertificate;
                timestamp.EndCertificate = timestampCertificate;
                signatureCertificate.PackageSignatures = new[] { packageSignature };
                timestampCertificate.TrustedTimestamps = new[] { timestamp };

                _validationContext.Mock(
                    validatorStatuses: new[] { validatorStatus },
                    packageSigningStates: new[] { packageSigningState },
                    packageSignatures: new[] { packageSignature },
                    endCertificates: new[] { signatureCertificate, timestampCertificate });

                var actual = await _target.StartAsync(_validationRequest.Object);

                _certificateVerifier.Verify(v => v.EnqueueVerificationAsync(It.IsAny<IValidationRequest>(), It.IsAny<EndCertificate>()), Times.Never);
                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);
                _telemetryService.Verify(
                    x => x.TrackDurationToStartPackageCertificatesValidator(_validationRequest.Object.PackageId, _validationRequest.Object.PackageVersion),
                    Times.Never);

                Assert.Equal(ValidationStatus.Failed, actual.Status);
                Assert.Equal(ValidationStatus.Failed, validatorStatus.State);
            }

            public static IEnumerable<object[]> OnRevalidationAllNonRevokedCertificatesAreVerifiedData()
            {
                // Certificate that has never been validated
                yield return new object[]
                {
                    new EndCertificate
                    {
                        Key = 123,
                        Status = EndCertificateStatus.Unknown,
                        StatusUpdateTime = null,
                        NextStatusUpdateTime = null,
                        LastVerificationTime = null,
                        RevocationTime = null,
                        ValidationFailures = 0,
                    },
                    2
                };

                // Certificate that was last validated a long time ago
                yield return new object[]
                {
                    new EndCertificate
                    {
                        Key = 123,
                        Status = EndCertificateStatus.Good,
                        StatusUpdateTime = DateTime.UtcNow.AddDays(-20),
                        NextStatusUpdateTime = DateTime.UtcNow.AddDays(1),
                        LastVerificationTime = DateTime.UtcNow.AddDays(-20),
                        RevocationTime = null,
                        ValidationFailures = 0,
                    },
                    2
                };

                // Certificate that was validated very recently
                yield return new object[]
                {
                    new EndCertificate
                    {
                        Key = 123,
                        Status = EndCertificateStatus.Good,
                        StatusUpdateTime = DateTime.UtcNow.AddSeconds(-10),
                        NextStatusUpdateTime = DateTime.UtcNow.AddDays(1),
                        LastVerificationTime = DateTime.UtcNow.AddDays(-20),
                        RevocationTime = null,
                        ValidationFailures = 0,
                    },
                    2
                };

                // Certificate that was revoked (but after the package was signed)
                yield return new object[]
                {
                    new EndCertificate
                    {
                        Key = 123,
                        Status = EndCertificateStatus.Revoked,
                        StatusUpdateTime = DateTime.UtcNow.AddDays(-20),
                        NextStatusUpdateTime = DateTime.UtcNow.AddDays(1),
                        LastVerificationTime = DateTime.UtcNow.AddDays(-20),
                        RevocationTime = DateTime.UtcNow.AddDays(-1),
                        ValidationFailures = 0,
                    },
                    1
                };

                // Certificate that was found to be invalid
                yield return new object[]
                {
                    new EndCertificate
                    {
                        Key = 123,
                        Status = EndCertificateStatus.Invalid,
                        StatusUpdateTime = DateTime.UtcNow.AddDays(-20),
                        NextStatusUpdateTime = DateTime.UtcNow.AddDays(1),
                        LastVerificationTime = DateTime.UtcNow.AddDays(-20),
                        RevocationTime = null,
                        ValidationFailures = 0,
                    },
                    2
                };
            }

            [Theory]
            [MemberData(nameof(OnRevalidationAllNonRevokedCertificatesAreVerifiedData))]
            public async Task OnRevalidationAllNonRevokedCertificatesAreVerified(
                EndCertificate signatureCertificate,
                int expectedCertificateValidations)
            {
                // Arrange
                var validatorStatus = new ValidatorStatus
                {
                    ValidationId = ValidationId,
                    ValidatorName = nameof(PackageCertificatesValidator),
                    PackageKey = PackageKey,
                    State = ValidationStatus.NotStarted,
                    ValidatorIssues = new List<ValidatorIssue>(),
                };

                var otherValidatorStatus = new ValidatorStatus
                {
                    ValidationId = OtherValidationId,
                    ValidatorName = nameof(PackageCertificatesValidator),
                    PackageKey = PackageKey,
                    State = ValidationStatus.Succeeded,
                    ValidatorIssues = new List<ValidatorIssue>(),
                };

                var packageSigningState = new PackageSigningState
                {
                    PackageKey = PackageKey,
                    PackageId = PackageId,
                    PackageNormalizedVersion = PackageNormalizedVersion,
                    SigningStatus = PackageSigningStatus.Valid,
                };

                var packageSignature = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Valid,
                    Type = PackageSignatureType.Author,
                };

                var timestamp = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-10),
                };

                var timestampCertificate = new EndCertificate
                {
                    Key = 123,
                    Status = EndCertificateStatus.Unknown,
                    StatusUpdateTime = null,
                    NextStatusUpdateTime = null,
                    LastVerificationTime = null,
                    RevocationTime = null,
                    ValidationFailures = 0,
                };
                
                packageSigningState.PackageSignatures = new[] { packageSignature };
                packageSignature.PackageSigningState = packageSigningState;;
                packageSignature.TrustedTimestamps = new[] { timestamp };
                packageSignature.EndCertificate = signatureCertificate;
                timestamp.EndCertificate = timestampCertificate;
                signatureCertificate.PackageSignatures = new[] { packageSignature };
                timestampCertificate.TrustedTimestamps = new[] { timestamp };

                _validationContext.Mock(
                    validatorStatuses: new[] { validatorStatus , otherValidatorStatus },
                    packageSigningStates: new[] { packageSigningState },
                    packageSignatures: new[] { packageSignature },
                    endCertificates: new[] { signatureCertificate, timestampCertificate });

                // Act & Assert (NOTE: revoked certificates are NOT verified again but invalid certificates are)
                var actual = await _target.StartAsync(_validationRequest.Object);

                _certificateVerifier.Verify(v => v.EnqueueVerificationAsync(It.IsAny<IValidationRequest>(), It.IsAny<EndCertificate>()), Times.Exactly(expectedCertificateValidations));
                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);
                _telemetryService.Verify(
                    x => x.TrackDurationToStartPackageCertificatesValidator(_validationRequest.Object.PackageId, _validationRequest.Object.PackageVersion),
                    Times.Once);

                Assert.Equal(ValidationStatus.Incomplete, actual.Status);
                Assert.Equal(ValidationStatus.Incomplete, validatorStatus.State);
            }

            [Fact]
            public async Task RevokedSignaturesAreInvalidated()
            {
                // Arrange
                var validatorStatus = new ValidatorStatus
                {
                    ValidationId = ValidationId,
                    ValidatorName = nameof(PackageCertificatesValidator),
                    PackageKey = PackageKey,
                    State = ValidationStatus.NotStarted,
                    ValidatorIssues = new List<ValidatorIssue>(),
                };

                var packageSigningState = new PackageSigningState
                {
                    PackageKey = PackageKey,
                    PackageId = PackageId,
                    PackageNormalizedVersion = PackageNormalizedVersion,
                    SigningStatus = PackageSigningStatus.Valid,
                };

                var packageSignature = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Valid,
                    Type = PackageSignatureType.Author,
                };

                var timestamp = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-1),
                };

                var certificate = new EndCertificate
                {
                    Key = 123,
                    Status = EndCertificateStatus.Revoked,
                    StatusUpdateTime = DateTime.UtcNow.AddSeconds(-10),
                    NextStatusUpdateTime = DateTime.UtcNow.AddDays(1),
                    LastVerificationTime = DateTime.UtcNow.AddSeconds(-10),
                    RevocationTime = DateTime.UtcNow.AddDays(-10),
                    ValidationFailures = 0,
                };

                packageSigningState.PackageSignatures = new[] { packageSignature };
                packageSignature.PackageSigningState = packageSigningState;
                packageSignature.TrustedTimestamps = new[] { timestamp };
                packageSignature.EndCertificate = certificate;
                certificate.PackageSignatures = new[] { packageSignature };

                _validationContext.Mock(
                    validatorStatuses: new[] { validatorStatus },
                    packageSigningStates: new[] { packageSigningState },
                    packageSignatures: new[] { packageSignature },
                    endCertificates: new[] { certificate });

                // Act & Assert
                var actual = await _target.StartAsync(_validationRequest.Object);

                _certificateVerifier.Verify(v => v.EnqueueVerificationAsync(It.IsAny<IValidationRequest>(), It.IsAny<EndCertificate>()), Times.Never);
                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);
                _telemetryService.Verify(
                    x => x.TrackDurationToStartPackageCertificatesValidator(_validationRequest.Object.PackageId, _validationRequest.Object.PackageVersion),
                    Times.Never);

                Assert.Equal(ValidationStatus.Failed, actual.Status);
                Assert.Equal(ValidationStatus.Failed, validatorStatus.State);
                Assert.Equal(PackageSignatureStatus.Invalid, packageSignature.Status);
                Assert.Equal(PackageSigningStatus.Invalid, packageSigningState.SigningStatus);
            }

            [Theory]
            [InlineData(PackageSignatureType.Repository)]
            [InlineData((PackageSignatureType)0)]
            public async Task NonAuthorSignaturesAreIgnored(PackageSignatureType type)
            {
                // Arrange
                var validatorStatus = new ValidatorStatus
                {
                    ValidationId = ValidationId,
                    ValidatorName = nameof(PackageCertificatesValidator),
                    PackageKey = PackageKey,
                    State = ValidationStatus.NotStarted,
                    ValidatorIssues = new List<ValidatorIssue>(),
                };

                var packageSigningState = new PackageSigningState
                {
                    PackageKey = PackageKey,
                    PackageId = PackageId,
                    PackageNormalizedVersion = PackageNormalizedVersion,
                    SigningStatus = PackageSigningStatus.Valid,
                };

                var authorPackageSignature = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Valid,
                    Type = PackageSignatureType.Author,
                };

                var repositoryPackageSignature = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Unknown,
                    Type = type,
                };

                var timestamp = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-10)
                };

                var signatureCertificate = new EndCertificate
                {
                    Key = 123,
                    Status = EndCertificateStatus.Good,
                    StatusUpdateTime = DateTime.UtcNow.AddSeconds(-10),
                    NextStatusUpdateTime = DateTime.UtcNow.AddDays(1),
                    LastVerificationTime = DateTime.UtcNow.AddSeconds(-10),
                    RevocationTime = null,
                    ValidationFailures = 0,
                };

                var timestampCertificate = new EndCertificate
                {
                    Key = 456,
                    Status = EndCertificateStatus.Good,
                    StatusUpdateTime = DateTime.UtcNow.AddSeconds(-10),
                    NextStatusUpdateTime = DateTime.UtcNow.AddDays(1),
                    LastVerificationTime = DateTime.UtcNow.AddSeconds(-10),
                    RevocationTime = null,
                    ValidationFailures = 0,
                };

                packageSigningState.PackageSignatures = new[] { authorPackageSignature, repositoryPackageSignature };
                authorPackageSignature.PackageSigningState = packageSigningState;
                authorPackageSignature.TrustedTimestamps = new[] { timestamp };
                authorPackageSignature.EndCertificate = signatureCertificate;
                timestamp.EndCertificate = timestampCertificate;
                signatureCertificate.PackageSignatures = new[] { authorPackageSignature };
                timestampCertificate.TrustedTimestamps = new[] { timestamp };

                _validationContext.Mock(
                    validatorStatuses: new[] { validatorStatus },
                    packageSigningStates: new[] { packageSigningState },
                    packageSignatures: new[] { authorPackageSignature, repositoryPackageSignature },
                    trustedTimestamps: new[] { timestamp },
                    endCertificates: new[] { signatureCertificate, timestampCertificate });

                // Act & Assert
                var actual = await _target.StartAsync(_validationRequest.Object);

                _certificateVerifier.Verify(v => v.EnqueueVerificationAsync(It.IsAny<IValidationRequest>(), It.IsAny<EndCertificate>()), Times.Never);
                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);
                _telemetryService.Verify(
                    x => x.TrackDurationToStartPackageCertificatesValidator(_validationRequest.Object.PackageId, _validationRequest.Object.PackageVersion),
                    Times.Never);

                Assert.Equal(ValidationStatus.Succeeded, actual.Status);
                Assert.Equal(ValidationStatus.Succeeded, validatorStatus.State);
            }

            public static IEnumerable<object[]> ValidationStatusesThatAreStarted = validationStatusesThatAreStarted.Select(s => new object[] { s });
        }

        public abstract class FactsBase
        {
            protected readonly Mock<IValidationEntitiesContext> _validationContext;
            protected readonly Mock<IValidateCertificateEnqueuer> _certificateVerifier;
            protected readonly Mock<ITelemetryService> _telemetryService;
            protected readonly Mock<ILogger<PackageCertificatesValidator>> _logger;
            protected readonly Mock<IValidationRequest> _validationRequest;
            protected readonly PackageCertificatesValidator _target;

            public FactsBase()
            {
                _validationContext = new Mock<IValidationEntitiesContext>();
                _certificateVerifier = new Mock<IValidateCertificateEnqueuer>();
                _telemetryService = new Mock<ITelemetryService>();
                _logger = new Mock<ILogger<PackageCertificatesValidator>>();

                _validationRequest = new Mock<IValidationRequest>();
                _validationRequest.Setup(x => x.NupkgUrl).Returns(NupkgUrl);
                _validationRequest.Setup(x => x.PackageId).Returns(PackageId);
                _validationRequest.Setup(x => x.PackageKey).Returns(PackageKey);
                _validationRequest.Setup(x => x.PackageVersion).Returns(PackageNormalizedVersion);
                _validationRequest.Setup(x => x.ValidationId).Returns(ValidationId);

                var validatorStateServiceLogger = new Mock<ILogger<ValidatorStateService>>();
                var validatorStateService = new ValidatorStateService(
                    _validationContext.Object,
                    ValidatorName.PackageCertificate,
                    validatorStateServiceLogger.Object);

                _target = new PackageCertificatesValidator(
                        _validationContext.Object,
                        validatorStateService,
                        _certificateVerifier.Object,
                        _telemetryService.Object,
                        _logger.Object);
            }
        }
    }
}
