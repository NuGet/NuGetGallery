// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Validation.Orchestrator;
using Xunit;

namespace NuGet.Services.Validation
{
    public class ValidatorStateServiceFacts
    {
        private const int PackageKey = 1001;
        private const string PackageId = "NuGet.Versioning";
        private const string PackageVersion = "4.3.0.0-ALPHA+git";
        private static readonly Guid ValidationId = new Guid("fb9c0bac-3d4d-4cc7-ac2d-b3940e15b94d");
        private static readonly Guid OtherValidationId = new Guid("6593BD33-ABC0-4049-BDCF-915807F1D2B3");
        private const string NupkgUrl = "https://example/nuget.versioning/4.3.0/package.nupkg";

        private static readonly ValidationStatus[] possibleValidationStatuses = new ValidationStatus[]
        {
            ValidationStatus.NotStarted,
            ValidationStatus.Incomplete,
            ValidationStatus.Succeeded,
            ValidationStatus.Failed,
        };

        class AValidator : IValidator
        {
            public Task<ValidationStatus> GetStatusAsync(IValidationRequest request) => throw new NotImplementedException();
            public Task<ValidationStatus> StartValidationAsync(IValidationRequest request) => throw new NotImplementedException();
        }

        class BValidator : IValidator
        {
            public Task<ValidationStatus> GetStatusAsync(IValidationRequest request) => throw new NotImplementedException();
            public Task<ValidationStatus> StartValidationAsync(IValidationRequest request) => throw new NotImplementedException();
        }

        public class TheGetStatusMethod : FactsBase
        {
            [Fact]
            public async Task GetStatusReturnsNotStartedIfNoPersistedStatus()
            {
                // Arrange
                _validationContext.Mock();

                var stateService = new ValidatorStateService(_validationContext.Object);

                // Act & Assert
                var status = await stateService.GetStatusAsync<AValidator>(_validationRequest.Object);

                Assert.Equal(ValidationId, status.ValidationId);
                Assert.Equal(PackageKey, status.PackageKey);
                Assert.Equal(nameof(AValidator), status.ValidatorName);
                Assert.Equal(ValidationStatus.NotStarted, status.State);
            }

            [Fact]
            public async Task GetStatusIgnoresStatusOfOtherValidations()
            {
                // Arrange
                _validationContext.Mock(
                    validatorStatuses: new[]
                    {
                        new ValidatorStatus
                        {
                            ValidationId = OtherValidationId,
                            PackageKey = PackageKey,
                            ValidatorName = nameof(AValidator),
                            State = ValidationStatus.Succeeded,
                        }
                    });

                var stateService = new ValidatorStateService(_validationContext.Object);

                // Act & Assert
                var status = await stateService.GetStatusAsync<AValidator>(_validationRequest.Object);

                Assert.Equal(ValidationId, status.ValidationId);
                Assert.Equal(PackageKey, status.PackageKey);
                Assert.Equal(nameof(AValidator), status.ValidatorName);
                Assert.Equal(ValidationStatus.NotStarted, status.State);
            }

            [Theory]
            [MemberData(nameof(PossibleValidationStatuses))]
            public async Task GetStatusReturnsPersistedStatus(ValidationStatus status)
            {
                // Arrange
                _validationContext.Mock(
                    validatorStatuses: new[]
                    {
                        new ValidatorStatus
                        {
                            ValidationId = ValidationId,
                            PackageKey = PackageKey,
                            ValidatorName = nameof(AValidator),
                            State = status,
                        },

                        // This next status is for some "other" validation and should be ignored.
                        new ValidatorStatus
                        {
                            ValidationId = OtherValidationId,
                            PackageKey = PackageKey,
                            ValidatorName = nameof(AValidator),
                            State = ValidationStatus.Failed,
                        }
                    });

                var stateService = new ValidatorStateService(_validationContext.Object);

                // Act & Assert
                var persistedStatus = await stateService.GetStatusAsync<AValidator>(_validationRequest.Object);

                Assert.Equal(ValidationId, persistedStatus.ValidationId);
                Assert.Equal(PackageKey, persistedStatus.PackageKey);
                Assert.Equal(nameof(AValidator), persistedStatus.ValidatorName);
                Assert.Equal(status, persistedStatus.State);
            }

            [Fact]
            public async Task GetStatusThrowsOnInvalidPackageKey()
            {
                // Arrange
                var stateService = new ValidatorStateService(_validationContext.Object);

                _validationContext.Mock(validatorStatuses: new[]
                {
                    new ValidatorStatus
                    {
                        ValidationId = ValidationId,
                        PackageKey = PackageKey + 1,
                        ValidatorName = nameof(AValidator),
                        State = ValidationStatus.Incomplete,
                    }
                });

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentException>(async () => await stateService.GetStatusAsync<AValidator>(_validationRequest.Object));
            }

            [Fact]
            public async Task GetStatusThrowsOnInvalidValidatorName()
            {
                // Arrange
                var stateService = new ValidatorStateService(_validationContext.Object);

                _validationContext.Mock(validatorStatuses: new[]
                {
                    new ValidatorStatus
                    {
                        ValidationId = ValidationId,
                        PackageKey = PackageKey,
                        ValidatorName = nameof(AValidator) + "Bad",
                        State = ValidationStatus.Incomplete,
                    }
                });

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentException>(async () => await stateService.GetStatusAsync<AValidator>(_validationRequest.Object));
            }

            public static IEnumerable<object[]> PossibleValidationStatuses => possibleValidationStatuses.Select(s => new object[] { s });
        }

        public class TheIsRevalidationRequestMethod : FactsBase
        {
            [Fact]
            public async Task ReturnsFalseIfPackageHasNeverBeenValidated()
            {
                // Arrange
                _validationContext.Mock();

                var stateService = new ValidatorStateService(_validationContext.Object);

                // Act & Assert
                Assert.False(await stateService.IsRevalidationRequestAsync<AValidator>(_validationRequest.Object));
            }

            [Fact]
            public async Task ReturnsTrueIfPackageHasBeenValidatedByValidator()
            {
                // Arrange
                _validationContext.Mock(
                    validatorStatuses: new[]
                    {
                        // The package was validated in another validation request by AValidator.
                        new ValidatorStatus
                        {
                            ValidationId = OtherValidationId,
                            PackageKey = PackageKey,
                            ValidatorName = nameof(AValidator),
                            State = ValidationStatus.Succeeded,
                        }
                    });

                var stateService = new ValidatorStateService(_validationContext.Object);

                // Act & Assert
                Assert.True(await stateService.IsRevalidationRequestAsync<AValidator>(_validationRequest.Object));
            }

            [Fact]
            public async Task ReturnsFalseIfPackageHasBeenValidatedByOtherValidator()
            {
                // Arrange
                _validationContext.Mock(
                    validatorStatuses: new[]
                    {
                        // The package was validated in another validation request by BValidator.
                        new ValidatorStatus
                        {
                            ValidationId = OtherValidationId,
                            PackageKey = PackageKey,
                            ValidatorName = nameof(BValidator),
                            State = ValidationStatus.Succeeded,
                        }
                    });

                var stateService = new ValidatorStateService(_validationContext.Object);

                // Act & Assert
                Assert.False(await stateService.IsRevalidationRequestAsync<AValidator>(_validationRequest.Object));
            }

            [Fact]
            public async Task ReturnsTrueIfPackageHasBeenValidatedByMultipleValidators()
            {
                // Arrange
                _validationContext.Mock(
                    validatorStatuses: new[]
                    {
                        // The package was validated in another validation request by AValidator and BValidator.
                        new ValidatorStatus
                        {
                            ValidationId = OtherValidationId,
                            PackageKey = PackageKey,
                            ValidatorName = nameof(AValidator),
                            State = ValidationStatus.Succeeded,
                        },
                        new ValidatorStatus
                        {
                            ValidationId = OtherValidationId,
                            PackageKey = PackageKey,
                            ValidatorName = nameof(BValidator),
                            State = ValidationStatus.Succeeded,
                        }
                    });

                var stateService = new ValidatorStateService(_validationContext.Object);

                // Act & Assert
                Assert.True(await stateService.IsRevalidationRequestAsync<AValidator>(_validationRequest.Object));
            }

            [Fact]
            public async Task ReturnsFalseIfCurrentValidationIsOnlyValidationByValidator()
            {
                // Arrange
                _validationContext.Mock(
                    validatorStatuses: new[]
                    {
                        // The current validation request has been persisted, but, the current validator "AValidator"
                        // has never validated the current package before.
                        new ValidatorStatus
                        {
                            ValidationId = ValidationId,
                            PackageKey = PackageKey,
                            ValidatorName = nameof(AValidator),
                            State = ValidationStatus.NotStarted,
                        },
                        new ValidatorStatus
                        {
                            ValidationId = OtherValidationId,
                            PackageKey = PackageKey,
                            ValidatorName = nameof(BValidator),
                            State = ValidationStatus.Succeeded,
                        }
                    });

                var stateService = new ValidatorStateService(_validationContext.Object);

                // Act & Assert
                Assert.False(await stateService.IsRevalidationRequestAsync<AValidator>(_validationRequest.Object));
            }

            [Fact]
            public async Task ReturnsTrueIfPackageHasBeenValidatedByMultipleValidatorsAndCurrentValidationIsPersisted()
            {
                // Arrange
                _validationContext.Mock(
                    validatorStatuses: new[]
                    {
                        // The current validation request has been persisted, and, the package was validated in another
                        // validation request by AValidator and BValidator.
                        new ValidatorStatus
                        {
                            ValidationId = ValidationId,
                            PackageKey = PackageKey,
                            ValidatorName = nameof(AValidator),
                            State = ValidationStatus.NotStarted,
                        },
                        new ValidatorStatus
                        {
                            ValidationId = OtherValidationId,
                            PackageKey = PackageKey,
                            ValidatorName = nameof(AValidator),
                            State = ValidationStatus.Failed,
                        },
                        new ValidatorStatus
                        {
                            ValidationId = OtherValidationId,
                            PackageKey = PackageKey,
                            ValidatorName = nameof(BValidator),
                            State = ValidationStatus.Succeeded,
                        }
                    });

                var stateService = new ValidatorStateService(_validationContext.Object);

                // Act & Assert
                Assert.True(await stateService.IsRevalidationRequestAsync<AValidator>(_validationRequest.Object));
            }
        }

        public class TheAddStatusAsyncMethod : FactsBase
        {
            [Fact]
            public async Task AddStatusAsyncThrowsIfValidatorNameIsWrong()
            {
                // Arrange
                var stateService = new ValidatorStateService(_validationContext.Object);
                var validationStatus = new ValidatorStatus
                {
                    ValidationId = ValidationId,
                    PackageKey = PackageKey,
                    ValidatorName = nameof(BValidator),
                    State = ValidationStatus.Incomplete,
                };

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentException>(() => stateService.AddStatusAsync<AValidator>(validationStatus));
            }

            [Theory]
            [MemberData(nameof(PossibleValidationStatuses))]
            public async Task AddStatusAsyncMethodPersistsStatus(ValidationStatus status)
            {
                // Arrange
                var validatorName = nameof(AValidator);
                var validatorStatuses = new Mock<IDbSet<ValidatorStatus>>();
                var stateService = new ValidatorStateService(_validationContext.Object);

                _validationContext.Mock(validatorStatusesMock: validatorStatuses);

                // Act & Assert
                var result = await stateService.AddStatusAsync<AValidator>(new ValidatorStatus
                {
                    ValidationId = ValidationId,
                    PackageKey = PackageKey,
                    ValidatorName = validatorName,
                    State = status,
                });

                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);

                validatorStatuses
                    .Verify(statuses => statuses.Add(
                        It.Is<ValidatorStatus>(
                            s => s.ValidationId == ValidationId
                                && s.PackageKey == PackageKey
                                && s.ValidatorName == validatorName
                                && s.State == status)),
                    Times.Once);

                Assert.Equal(AddStatusResult.Success, result);
            }

            public static IEnumerable<object[]> PossibleValidationStatuses => possibleValidationStatuses.Select(s => new object[] { s });
        }

        public class TheSaveStatusAsyncMethod : FactsBase
        {
            [Fact]
            public async Task SaveStatusAsyncThrowsIfValidatorNameIsWrong()
            {
                // Arrange
                var stateService = new ValidatorStateService(_validationContext.Object);
                var validatorStatus = new ValidatorStatus
                {
                    ValidationId = ValidationId,
                    PackageKey = PackageKey,
                    ValidatorName = nameof(BValidator),
                    State = ValidationStatus.NotStarted,
                };

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentException>(() => stateService.AddStatusAsync<AValidator>(validatorStatus));
            }

            [Theory]
            [MemberData(nameof(PossibleValidationStatuses))]
            public async Task SaveStatusAsyncMethodPersistsStatus(ValidationStatus status)
            {
                // Arrange
                var stateService = new ValidatorStateService(_validationContext.Object);
                var validatorStatus = new ValidatorStatus
                {
                    ValidationId = ValidationId,
                    PackageKey = PackageKey,
                    ValidatorName = nameof(AValidator),
                    State = ValidationStatus.NotStarted,
                };

                _validationContext.Mock();

                // Act & Assert
                await stateService.AddStatusAsync<AValidator>(validatorStatus);

                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);
            }

            public static IEnumerable<object[]> PossibleValidationStatuses => possibleValidationStatuses.Select(s => new object[] { s });
        }

        public abstract class FactsBase
        {
            protected readonly Mock<IValidationEntitiesContext> _validationContext;
            protected readonly Mock<IValidationRequest> _validationRequest;

            public FactsBase()
            {
                _validationContext = new Mock<IValidationEntitiesContext>();

                _validationRequest = new Mock<IValidationRequest>();
                _validationRequest.Setup(x => x.NupkgUrl).Returns(NupkgUrl);
                _validationRequest.Setup(x => x.PackageId).Returns(PackageId);
                _validationRequest.Setup(x => x.PackageKey).Returns(PackageKey);
                _validationRequest.Setup(x => x.PackageVersion).Returns(PackageVersion);
                _validationRequest.Setup(x => x.ValidationId).Returns(ValidationId);
            }
        }
    }
}
