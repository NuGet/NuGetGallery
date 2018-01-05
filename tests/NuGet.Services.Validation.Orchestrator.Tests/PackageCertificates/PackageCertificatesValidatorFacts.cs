// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Services.Validation.PackageCertificates;
using Validation.PackageSigning.Helpers;
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
                    new EndCertificateStatus?[] { null },
                };

                yield return new object[]
                {
                    ValidationStatus.Incomplete,
                    new EndCertificateStatus?[] { EndCertificateStatus.Good, null, EndCertificateStatus.Revoked },
                };

                // If ALL certificate validations have a status of "Good" or "Unknown", the overall validation
                // should be "Succeeded".
                yield return new object[]
                {
                    ValidationStatus.Succeeded,
                    new EndCertificateStatus?[] { EndCertificateStatus.Good },
                };

                yield return new object[]
                {
                    ValidationStatus.Succeeded,
                    new EndCertificateStatus?[] { EndCertificateStatus.Unknown },
                };

                yield return new object[]
                {
                    ValidationStatus.Succeeded,
                    new EndCertificateStatus?[] { EndCertificateStatus.Good, EndCertificateStatus.Unknown, EndCertificateStatus.Good },
                };

                // The validator should be resilient to no certificates. Since this tests packages that were marked as
                // signed, this should NEVER happen.
                yield return new object[]
                {
                    ValidationStatus.Succeeded,
                    new EndCertificateStatus?[] { },
                };
            }

            [Theory]
            [MemberData(nameof(GetReturnsExpectedStatusForCertificateValidationsData))]
            public async Task ReturnsExpectedStatusForCertificateValidations(ValidationStatus expectedStatus, IEnumerable<EndCertificateStatus?> certificateStatuses)
            {
                // Arrange
                var certificates = new List<EndCertificate>();
                var certificateValidations = new List<EndCertificateValidation>();
                var packageSignatures = new List<PackageSignature>();

                foreach (var certificateStatus in certificateStatuses)
                {
                    var timestamp = new TrustedTimestamp
                    {
                        Value = DateTime.UtcNow
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
                    };

                    certificate.PackageSignatures = new[] { packageSignature };
                    certificate.Validations = new[] { certificateValidation };

                    packageSignature.TrustedTimestamps = new[] { timestamp };

                    certificates.Add(certificate);
                    certificateValidations.Add(certificateValidation);
                    packageSignatures.Add(packageSignature);
                }

                _validationContext.Mock(
                    validatorStatuses: new ValidatorStatus[]
                    {
                        new ValidatorStatus
                        {
                            ValidationId = ValidationId,
                            ValidatorName = nameof(PackageCertificatesValidator),
                            PackageKey = PackageKey,
                            State = ValidationStatus.Incomplete,
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

            public static IEnumerable<object[]> InvalidSignaturesFailsValidationData()
            {
                // Signatures SHOULD NOT have "Valid" and "InGracePeriod" Statuses before
                // the CertificateValidator finishes. If the signatures somehow do, the
                // validator should fail as this is an invalid state.
                yield return new object[]
                {
                    ValidationStatus.Failed, new[] { PackageSignatureStatus.Valid }
                };

                yield return new object[]
                {
                    ValidationStatus.Failed, new[] { PackageSignatureStatus.InGracePeriod }
                };

                yield return new object[]
                {
                    ValidationStatus.Succeeded, new[] { PackageSignatureStatus.Unknown }
                };

                yield return new object[]
                {
                    ValidationStatus.Failed, new[] { PackageSignatureStatus.Invalid }
                };

                yield return new object[]
                {
                    ValidationStatus.Failed,
                    new[]
                    {
                        PackageSignatureStatus.Unknown,
                        PackageSignatureStatus.InGracePeriod,
                    }
                };

                yield return new object[]
                {
                    ValidationStatus.Failed,
                    new[]
                    {
                        PackageSignatureStatus.Unknown,
                        PackageSignatureStatus.Invalid,
                    }
                };
            }

            [Theory]
            [MemberData(nameof(InvalidSignaturesFailsValidationData))]
            public async Task InvalidSignaturesFailsValidation(
                ValidationStatus expectedStatus,
                PackageSignatureStatus[] packageSignatureStatuses)
            {
                var validatorStatus = new ValidatorStatus
                {
                    ValidationId = ValidationId,
                    ValidatorName = nameof(PackageCertificatesValidator),
                    PackageKey = PackageKey,
                    State = ValidationStatus.Incomplete,
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

                foreach (var status in packageSignatureStatuses)
                {
                    var signature = new PackageSignature
                    {
                        PackageKey = PackageKey,
                        Status = status,
                        PackageSigningState = packageSigningState,
                        EndCertificate = certificate,
                    };

                    packageSigningState.PackageSignatures.Add(signature);
                    certificate.PackageSignatures.Add(signature);
                }

                certificate.Validations = new[] { certificateValidation };

                _validationContext.Mock(
                    validatorStatuses: new[] { validatorStatus },
                    packageSigningStates: new[] { packageSigningState },
                    packageSignatures: certificate.PackageSignatures,
                    endCertificates: new[] { certificate },
                    certificateValidations: new[] { certificateValidation });

                // Act & Assert
                var result = await _target.GetResultAsync(_validationRequest.Object);

                Assert.Equal(expectedStatus, result.Status);
            }

            public static IEnumerable<object[]> ValidSignaturesArePromotedData()
            {
                var timestamp1DayAgo = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-1),
                };

                var timestamp10YearsAgo = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-10),
                };

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

                // A signature whose timestamp is before the certificate's last update should be
                // promoted to "Valid".
                yield return new object[]
                {
                    PackageSignatureStatus.Valid,
                    new PackageSignature
                    {
                        EndCertificate = cert1SecondAgo,
                        TrustedTimestamps = new[] { timestamp1DayAgo },
                    },
                };

                // A signature whose timestamp is after the certificate's last update should be
                // promoted to "InGracePeriod"
                yield return new object[]
                {
                    PackageSignatureStatus.InGracePeriod,
                    new PackageSignature
                    {
                        EndCertificate = cert1YearAgo,
                        TrustedTimestamps = new[] { timestamp1DayAgo },
                    },
                };

                // The oldest timestamp should be used for promotion decisions.
                yield return new object[]
                {
                    PackageSignatureStatus.InGracePeriod,
                    new PackageSignature
                    {
                        EndCertificate = cert1YearAgo,
                        TrustedTimestamps = new[] { timestamp1DayAgo, timestamp10YearsAgo },
                    },
                };

                // A signature whose certificate is revoked should be promoted as "Valid" as long as the revocation
                // time begins after the package was signed.
                yield return new object[]
                {
                    PackageSignatureStatus.Valid,
                    new PackageSignature
                    {
                        EndCertificate = certRevoked1SecondAgo,
                        TrustedTimestamps = new[] { timestamp1DayAgo }
                    }
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
                    }
                });

                // Act & Assert
                var actual = await _target.StartValidationAsync(_validationRequest.Object);

                _certificateVerifier.Verify(v => v.EnqueueVerificationAsync(It.IsAny<IValidationRequest>(), It.IsAny<EndCertificate>()), Times.Never);
                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Never);

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
                var actual = await _target.StartValidationAsync(_validationRequest.Object);

                _certificateVerifier.Verify(v => v.EnqueueVerificationAsync(It.IsAny<IValidationRequest>(), It.IsAny<EndCertificate>()), Times.Never);
                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);

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
                    Status = PackageSignatureStatus.Valid
                };

                var timestamp = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-10)
                };

                var certificate = new EndCertificate
                {
                    Key = 123,
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
                packageSignature.EndCertificate = certificate;
                certificate.PackageSignatures = new[] { packageSignature };

                _validationContext.Mock(
                    validatorStatuses: new[] { validatorStatus },
                    packageSigningStates: new[] { packageSigningState },
                    packageSignatures: new[] { packageSignature },
                    endCertificates: new[] { certificate });

                // Act & Assert
                var actual = await _target.StartValidationAsync(_validationRequest.Object);

                _certificateVerifier.Verify(v => v.EnqueueVerificationAsync(It.IsAny<IValidationRequest>(), It.IsAny<EndCertificate>()), Times.Never);
                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);

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
                };

                var packageSigningState = new PackageSigningState
                {
                    PackageKey = PackageKey,
                    PackageId = PackageId,
                    PackageNormalizedVersion = PackageNormalizedVersion,
                    SigningStatus = PackageSigningStatus.Valid,
                };

                var packageSignature1 = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Valid
                };

                var timestamp1 = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-10),
                };

                var certificate1 = new EndCertificate
                {
                    Key = 123,
                    Status = EndCertificateStatus.Good,
                    StatusUpdateTime = null,
                    NextStatusUpdateTime = null,
                    LastVerificationTime = null,
                    RevocationTime = null,
                    ValidationFailures = 0,
                };

                var packageSignature2 = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Valid
                };

                var timestamp2 = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-10),
                };

                var certificate2 = new EndCertificate
                {
                    Key = 123,
                    Status = EndCertificateStatus.Good,
                    StatusUpdateTime = DateTime.UtcNow.AddDays(-20),
                    NextStatusUpdateTime = DateTime.UtcNow.AddDays(1),
                    LastVerificationTime = DateTime.UtcNow.AddDays(-20),
                    RevocationTime = null,
                    ValidationFailures = 0,
                };

                var packageSignature3 = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Valid
                };

                var timestamp3 = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-10),
                };

                var certificate3 = new EndCertificate
                {
                    Key = 123,
                    Status = EndCertificateStatus.Good,
                    StatusUpdateTime = DateTime.UtcNow.AddSeconds(-10),
                    NextStatusUpdateTime = DateTime.UtcNow.AddDays(1),
                    LastVerificationTime = DateTime.UtcNow.AddSeconds(-10),
                    RevocationTime = null,
                    ValidationFailures = 0,
                };

                packageSigningState.PackageSignatures = new[] { packageSignature1 };
                packageSignature1.PackageSigningState = packageSigningState;
                packageSignature2.PackageSigningState = packageSigningState;
                packageSignature3.PackageSigningState = packageSigningState;
                packageSignature1.TrustedTimestamps = new[] { timestamp1 };
                packageSignature2.TrustedTimestamps = new[] { timestamp2 };
                packageSignature3.TrustedTimestamps = new[] { timestamp3 };
                packageSignature1.EndCertificate = certificate1;
                packageSignature2.EndCertificate = certificate2;
                packageSignature3.EndCertificate = certificate3;
                certificate1.PackageSignatures = new[] { packageSignature1 };
                certificate2.PackageSignatures = new[] { packageSignature2 };
                certificate3.PackageSignatures = new[] { packageSignature3 };

                _validationContext.Mock(
                    validatorStatuses: new[] { validatorStatus },
                    packageSigningStates: new[] { packageSigningState },
                    packageSignatures: new[] { packageSignature1, packageSignature2, packageSignature3 },
                    endCertificates: new[] { certificate1, certificate2, certificate3 });

                // Act & Assert (NOTE: recently validated certificates are NOT verified again)
                var actual = await _target.StartValidationAsync(_validationRequest.Object);

                _certificateVerifier.Verify(v => v.EnqueueVerificationAsync(It.IsAny<IValidationRequest>(), It.IsAny<EndCertificate>()), Times.Exactly(2));
                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);

                Assert.Equal(ValidationStatus.Incomplete, actual.Status);
                Assert.Equal(ValidationStatus.Incomplete, validatorStatus.State);
            }

            [Fact]
            public async Task CertificatesRevokedAfterPackageWasSignedDoesntInvalidateSignature()
            {
                // Arrange
                var validatorStatus = new ValidatorStatus
                {
                    ValidationId = ValidationId,
                    ValidatorName = nameof(PackageCertificatesValidator),
                    PackageKey = PackageKey,
                    State = ValidationStatus.NotStarted,
                };

                var packageSigningState = new PackageSigningState
                {
                    PackageKey = PackageKey,
                    PackageId = PackageId,
                    PackageNormalizedVersion = PackageNormalizedVersion,
                    SigningStatus = PackageSigningStatus.Valid,
                };

                var packageSignature1 = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Valid
                };

                var timestamp1 = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-10),
                };

                var certificate1 = new EndCertificate
                {
                    Key = 123,
                    Status = EndCertificateStatus.Good,
                    StatusUpdateTime = null,
                    NextStatusUpdateTime = null,
                    LastVerificationTime = null,
                    RevocationTime = null,
                    ValidationFailures = 0,
                };

                var packageSignature2 = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Valid
                };

                var timestamp2 = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-10),
                };

                var certificate2 = new EndCertificate
                {
                    Key = 123,
                    Status = EndCertificateStatus.Revoked,
                    StatusUpdateTime = DateTime.UtcNow.AddDays(-20),
                    NextStatusUpdateTime = DateTime.UtcNow.AddDays(1),
                    LastVerificationTime = DateTime.UtcNow.AddDays(-20),
                    RevocationTime = DateTime.UtcNow.AddDays(-1),
                    ValidationFailures = 0,
                };

                packageSigningState.PackageSignatures = new[] { packageSignature1, packageSignature2 };
                packageSignature1.PackageSigningState = packageSigningState;
                packageSignature2.PackageSigningState = packageSigningState;
                packageSignature1.EndCertificate = certificate1;
                packageSignature2.EndCertificate = certificate2;
                packageSignature1.TrustedTimestamps = new[] { timestamp1 };
                packageSignature2.TrustedTimestamps = new[] { timestamp2 };
                certificate1.PackageSignatures = new[] { packageSignature1 };
                certificate2.PackageSignatures = new[] { packageSignature2 };

                _validationContext.Mock(
                    validatorStatuses: new[] { validatorStatus },
                    packageSigningStates: new[] { packageSigningState },
                    packageSignatures: new[] { packageSignature1, packageSignature2 },
                    endCertificates: new[] { certificate1, certificate2 });

                // Act & Assert (NOTE: the "Revoked" certificate must NOT be verified!)
                var actual = await _target.StartValidationAsync(_validationRequest.Object);

                _certificateVerifier.Verify(v => v.EnqueueVerificationAsync(It.IsAny<IValidationRequest>(), It.IsAny<EndCertificate>()), Times.Once);
                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);

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
                };

                var packageSigningState = new PackageSigningState
                {
                    PackageKey = PackageKey,
                    PackageId = PackageId,
                    PackageNormalizedVersion = PackageNormalizedVersion,
                    SigningStatus = PackageSigningStatus.Valid,
                };

                var packageSignature1 = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Valid
                };

                var timestamp1 = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-10),
                };

                var certificate1 = new EndCertificate
                {
                    Key = 123,
                    Status = EndCertificateStatus.Good,
                    StatusUpdateTime = null,
                    NextStatusUpdateTime = null,
                    LastVerificationTime = null,
                    RevocationTime = null,
                    ValidationFailures = 0,
                };

                var packageSignature2 = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Valid
                };

                var timestamp2 = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-10),
                };

                var certificate2 = new EndCertificate
                {
                    Key = 123,
                    Status = EndCertificateStatus.Invalid,
                    StatusUpdateTime = DateTime.UtcNow.AddDays(-20),
                    NextStatusUpdateTime = DateTime.UtcNow.AddDays(1),
                    LastVerificationTime = DateTime.UtcNow.AddDays(-20),
                    RevocationTime = null,
                    ValidationFailures = 0,
                };

                packageSigningState.PackageSignatures = new[] { packageSignature1, packageSignature2 };
                packageSignature1.PackageSigningState = packageSigningState;
                packageSignature2.PackageSigningState = packageSigningState;
                packageSignature1.TrustedTimestamps = new[] { timestamp1 };
                packageSignature2.TrustedTimestamps = new[] { timestamp2 };
                packageSignature1.EndCertificate = certificate1;
                packageSignature2.EndCertificate = certificate2;
                certificate1.PackageSignatures = new[] { packageSignature1 };
                certificate2.PackageSignatures = new[] { packageSignature2 };

                _validationContext.Mock(
                    validatorStatuses: new[] { validatorStatus },
                    packageSigningStates: new[] { packageSigningState },
                    packageSignatures: new[] { packageSignature1, packageSignature2 },
                    endCertificates: new[] { certificate1, certificate2 });

                var actual = await _target.StartValidationAsync(_validationRequest.Object);

                _certificateVerifier.Verify(v => v.EnqueueVerificationAsync(It.IsAny<IValidationRequest>(), It.IsAny<EndCertificate>()), Times.Never);
                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);

                Assert.Equal(ValidationStatus.Failed, actual.Status);
                Assert.Equal(ValidationStatus.Failed, validatorStatus.State);
            }

            [Fact]
            public async Task OnRevalidationAllNonRevokedCertificatesAreVerified()
            {
                // Arrange
                var validatorStatus = new ValidatorStatus
                {
                    ValidationId = ValidationId,
                    ValidatorName = nameof(PackageCertificatesValidator),
                    PackageKey = PackageKey,
                    State = ValidationStatus.NotStarted,
                };

                var otherValidatorStatus = new ValidatorStatus
                {
                    ValidationId = OtherValidationId,
                    ValidatorName = nameof(PackageCertificatesValidator),
                    PackageKey = PackageKey,
                    State = ValidationStatus.Succeeded,
                };

                var packageSigningState = new PackageSigningState
                {
                    PackageKey = PackageKey,
                    PackageId = PackageId,
                    PackageNormalizedVersion = PackageNormalizedVersion,
                    SigningStatus = PackageSigningStatus.Valid,
                };

                // Certificate that has never been validated
                var packageSignature1 = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Valid
                };

                var timestamp1 = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-10),
                };

                var certificate1 = new EndCertificate
                {
                    Key = 123,
                    Status = EndCertificateStatus.Good,
                    StatusUpdateTime = null,
                    NextStatusUpdateTime = null,
                    LastVerificationTime = null,
                    RevocationTime = null,
                    ValidationFailures = 0,
                };

                // Certificate that was last validated a long time ago
                var packageSignature2 = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Valid
                };

                var timestamp2 = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-10),
                };

                var certificate2 = new EndCertificate
                {
                    Key = 123,
                    Status = EndCertificateStatus.Good,
                    StatusUpdateTime = DateTime.UtcNow.AddDays(-20),
                    NextStatusUpdateTime = DateTime.UtcNow.AddDays(1),
                    LastVerificationTime = DateTime.UtcNow.AddDays(-20),
                    RevocationTime = null,
                    ValidationFailures = 0,
                };

                // Certificate that was validated very recently
                var packageSignature3 = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Valid
                };

                var timestamp3 = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-10),
                };

                var certificate3 = new EndCertificate
                {
                    Key = 123,
                    Status = EndCertificateStatus.Good,
                    StatusUpdateTime = DateTime.UtcNow.AddSeconds(-10),
                    NextStatusUpdateTime = DateTime.UtcNow.AddDays(1),
                    LastVerificationTime = DateTime.UtcNow.AddSeconds(-10),
                    RevocationTime = null,
                    ValidationFailures = 0,
                };

                // Certificate that was revoked (but after the package was signed)
                var packageSignature4 = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Valid
                };

                var timestamp4 = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-10),
                };

                var certificate4 = new EndCertificate
                {
                    Key = 123,
                    Status = EndCertificateStatus.Revoked,
                    StatusUpdateTime = DateTime.UtcNow.AddDays(-20),
                    NextStatusUpdateTime = DateTime.UtcNow.AddDays(1),
                    LastVerificationTime = DateTime.UtcNow.AddDays(-20),
                    RevocationTime = DateTime.UtcNow.AddDays(-1),
                    ValidationFailures = 0,
                };

                // Certificate that was found to be invalid
                var packageSignature5 = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Valid
                };

                var timestamp5 = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-10),
                };

                var certificate5 = new EndCertificate
                {
                    Key = 123,
                    Status = EndCertificateStatus.Invalid,
                    StatusUpdateTime = DateTime.UtcNow.AddDays(-20),
                    NextStatusUpdateTime = DateTime.UtcNow.AddDays(1),
                    LastVerificationTime = DateTime.UtcNow.AddDays(-20),
                    RevocationTime = null,
                    ValidationFailures = 0,
                };

                packageSigningState.PackageSignatures = new[] { packageSignature1, packageSignature2, packageSignature3, packageSignature4 };
                packageSignature1.PackageSigningState = packageSigningState;
                packageSignature2.PackageSigningState = packageSigningState;
                packageSignature3.PackageSigningState = packageSigningState;
                packageSignature4.PackageSigningState = packageSigningState;
                packageSignature5.PackageSigningState = packageSigningState;
                packageSignature1.TrustedTimestamps = new[] { timestamp1 };
                packageSignature2.TrustedTimestamps = new[] { timestamp2 };
                packageSignature3.TrustedTimestamps = new[] { timestamp3 };
                packageSignature4.TrustedTimestamps = new[] { timestamp4 };
                packageSignature5.TrustedTimestamps = new[] { timestamp5 };
                packageSignature1.EndCertificate = certificate1;
                packageSignature2.EndCertificate = certificate2;
                packageSignature3.EndCertificate = certificate3;
                packageSignature4.EndCertificate = certificate4;
                packageSignature5.EndCertificate = certificate5;
                certificate1.PackageSignatures = new[] { packageSignature1 };
                certificate2.PackageSignatures = new[] { packageSignature2 };
                certificate3.PackageSignatures = new[] { packageSignature3 };
                certificate4.PackageSignatures = new[] { packageSignature4 };
                certificate5.PackageSignatures = new[] { packageSignature5 };

                _validationContext.Mock(
                    validatorStatuses: new[] { validatorStatus, otherValidatorStatus },
                    packageSigningStates: new[] { packageSigningState },
                    packageSignatures: new[] { packageSignature1, packageSignature2, packageSignature3, packageSignature4, packageSignature5 },
                    endCertificates: new[] { certificate1, certificate2, certificate3, certificate4, certificate5 });

                // Act & Assert (NOTE: revoked certificates are NOT verified again but invalid certificates are)
                var actual = await _target.StartValidationAsync(_validationRequest.Object);

                _certificateVerifier.Verify(v => v.EnqueueVerificationAsync(It.IsAny<IValidationRequest>(), It.IsAny<EndCertificate>()), Times.Exactly(4));
                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);

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
                };

                var packageSigningState = new PackageSigningState
                {
                    PackageKey = PackageKey,
                    PackageId = PackageId,
                    PackageNormalizedVersion = PackageNormalizedVersion,
                    SigningStatus = PackageSigningStatus.Valid,
                };

                var packageSignature1 = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Valid,
                };

                var timestamp1 = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-1),
                };

                var packageSignature2 = new PackageSignature
                {
                    PackageKey = PackageKey,
                    Status = PackageSignatureStatus.Valid,
                };

                var timestamp2 = new TrustedTimestamp
                {
                    Value = DateTime.UtcNow.AddDays(-1),
                };

                var certificate1 = new EndCertificate
                {
                    Key = 123,
                    Status = EndCertificateStatus.Revoked,
                    StatusUpdateTime = DateTime.UtcNow.AddSeconds(-10),
                    NextStatusUpdateTime = DateTime.UtcNow.AddDays(1),
                    LastVerificationTime = DateTime.UtcNow.AddSeconds(-10),
                    RevocationTime = DateTime.UtcNow.AddDays(-10),
                    ValidationFailures = 0,
                };

                var certificate2 = new EndCertificate
                {
                    Key = 123,
                    Status = EndCertificateStatus.Good,
                    StatusUpdateTime = null,
                    NextStatusUpdateTime = null,
                    LastVerificationTime = null,
                    RevocationTime = null,
                    ValidationFailures = 0,
                };

                packageSigningState.PackageSignatures = new[] { packageSignature1, packageSignature2 };
                packageSignature1.PackageSigningState = packageSigningState;
                packageSignature2.PackageSigningState = packageSigningState;
                packageSignature1.TrustedTimestamps = new[] { timestamp1 };
                packageSignature2.TrustedTimestamps = new[] { timestamp2 };
                packageSignature1.EndCertificate = certificate1;
                packageSignature2.EndCertificate = certificate2;
                certificate1.PackageSignatures = new[] { packageSignature1 };
                certificate2.PackageSignatures = new[] { packageSignature2 };

                _validationContext.Mock(
                    validatorStatuses: new[] { validatorStatus },
                    packageSigningStates: new[] { packageSigningState },
                    packageSignatures: new[] { packageSignature1, packageSignature2 },
                    endCertificates: new[] { certificate1, certificate2 });

                // Act & Assert
                var actual = await _target.StartValidationAsync(_validationRequest.Object);

                _certificateVerifier.Verify(v => v.EnqueueVerificationAsync(It.IsAny<IValidationRequest>(), It.IsAny<EndCertificate>()), Times.Never);
                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);

                Assert.Equal(ValidationStatus.Failed, actual.Status);
                Assert.Equal(ValidationStatus.Failed, validatorStatus.State);
                Assert.Equal(PackageSignatureStatus.Invalid, packageSignature1.Status);
                Assert.Equal(PackageSignatureStatus.Valid, packageSignature2.Status);
                Assert.Equal(PackageSigningStatus.Invalid, packageSigningState.SigningStatus);
            }

            public static IEnumerable<object[]> ValidationStatusesThatAreStarted = validationStatusesThatAreStarted.Select(s => new object[] { s });
        }

        public abstract class FactsBase
        {
            protected readonly Mock<IValidationEntitiesContext> _validationContext;
            protected readonly Mock<ICertificateVerificationEnqueuer> _certificateVerifier;
            protected readonly Mock<ILogger<PackageCertificatesValidator>> _logger;
            protected readonly Mock<IValidationRequest> _validationRequest;
            protected readonly PackageCertificatesValidator _target;

            public FactsBase()
            {
                _validationContext = new Mock<IValidationEntitiesContext>();
                _certificateVerifier = new Mock<ICertificateVerificationEnqueuer>();
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
                    typeof(PackageCertificatesValidator),
                    validatorStateServiceLogger.Object);

                _target = new PackageCertificatesValidator(
                        _validationContext.Object,
                        validatorStateService,
                        _certificateVerifier.Object,
                        _logger.Object);
            }
        }
    }
}
