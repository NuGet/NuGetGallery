// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Storage;
using Tests.ContextHelpers;
using Xunit;
using Xunit.Abstractions;

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
        private static readonly Uri InputUrl = new Uri("https://example/vsix.versioning/4.3.0/package.vsix");
        private static readonly Guid ValidationId1 = new Guid("fc9c0bac-3d4d-4cc7-ac2d-b3940e15b94d");
        private static readonly Guid OtherValidationId1 = new Guid("6693BD33-ABC0-4049-BDCF-915807F1D2B3");

        private static readonly ValidationStatus[] possibleValidationStatuses = new ValidationStatus[]
        {
            ValidationStatus.NotStarted,
            ValidationStatus.Incomplete,
            ValidationStatus.Succeeded,
            ValidationStatus.Failed,
        };

        [ValidatorName("AValidator")]
        class AValidator : INuGetValidator
        {
            public Task CleanUpAsync(INuGetValidationRequest request) => throw new NotImplementedException();
            public Task<INuGetValidationResponse> GetResponseAsync(INuGetValidationRequest request) => throw new NotImplementedException();
            public Task<INuGetValidationResponse> StartAsync(INuGetValidationRequest request) => throw new NotImplementedException();
        }

        [ValidatorName("BValidator")]
        class BValidator : INuGetValidator
        {
            public Task CleanUpAsync(INuGetValidationRequest request) => throw new NotImplementedException();
            public Task<INuGetValidationResponse> GetResponseAsync(INuGetValidationRequest request) => throw new NotImplementedException();
            public Task<INuGetValidationResponse> StartAsync(INuGetValidationRequest request) => throw new NotImplementedException();
        }

        [ValidatorName("CValidator")]
        class CValidator : IValidator
        {
            public Task CleanUpAsync(IValidationRequest request) => throw new NotImplementedException();
            public Task<IValidationResponse> GetResponseAsync(IValidationRequest request) => throw new NotImplementedException();
            public Task<IValidationResponse> StartAsync(IValidationRequest request) => throw new NotImplementedException();
        }

        public class TheGetStatusMethod : FactsBase
        {
            public TheGetStatusMethod(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task GetStatusReturnsNotStartedIfNoPersistedStatus()
            {
                // Arrange
                _validationContext.Mock();

                // Act & Assert
                var status = await _target.GetStatusAsync(_validationRequest.Object);

                Assert.Equal(ValidationId, status.ValidationId);
                Assert.Equal(PackageKey, status.PackageKey);
                Assert.Equal(nameof(AValidator), status.ValidatorName);
                Assert.Equal(ValidationStatus.NotStarted, status.State);
                Assert.NotNull(status.ValidatorIssues);
                Assert.Empty(status.ValidatorIssues);
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

                // Act & Assert
                var status = await _target.GetStatusAsync(_validationRequest.Object);

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

                // Act & Assert
                var persistedStatus = await _target.GetStatusAsync(_validationRequest.Object);

                Assert.Equal(ValidationId, persistedStatus.ValidationId);
                Assert.Equal(PackageKey, persistedStatus.PackageKey);
                Assert.Equal(nameof(AValidator), persistedStatus.ValidatorName);
                Assert.Equal(status, persistedStatus.State);
            }

            [Fact]
            public async Task GetStatusThrowsOnInvalidPackageKey()
            {
                // Arrange
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
                await Assert.ThrowsAsync<ArgumentException>(async () => await _target.GetStatusAsync(_validationRequest.Object));
            }

            [Fact]
            public async Task GetStatusThrowsOnInvalidValidatorName()
            {
                // Arrange
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
                await Assert.ThrowsAsync<ArgumentException>(async () => await _target.GetStatusAsync(_validationRequest.Object));
            }

            public static IEnumerable<object[]> PossibleValidationStatuses => possibleValidationStatuses.Select(s => new object[] { s });
        }

        public class TheIsRevalidationRequestMethod : FactsBase
        {
            public TheIsRevalidationRequestMethod(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task ReturnsFalseIfPackageHasNeverBeenValidated()
            {
                // Arrange
                _validationContext.Mock();

                // Act & Assert
                Assert.False(await _target.IsRevalidationRequestAsync(_validationRequest.Object, ValidatingType.Package));
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

                // Act & Assert
                Assert.True(await _target.IsRevalidationRequestAsync(_validationRequest.Object, ValidatingType.Package));
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

                // Act & Assert
                Assert.False(await _target.IsRevalidationRequestAsync(_validationRequest.Object, ValidatingType.Package));
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

                // Act & Assert
                Assert.True(await _target.IsRevalidationRequestAsync(_validationRequest.Object, ValidatingType.Package));
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

                // Act & Assert
                Assert.False(await _target.IsRevalidationRequestAsync(_validationRequest.Object, ValidatingType.Package));
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

                // Act & Assert
                Assert.True(await _target.IsRevalidationRequestAsync(_validationRequest.Object, ValidatingType.Package));
            }
        }

        public class TheAddStatusAsyncMethod : FactsBase
        {
            public TheAddStatusAsyncMethod(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task AddStatusAsyncThrowsIfValidatorNameIsWrong()
            {
                // Arrange
                var validationStatus = new ValidatorStatus
                {
                    ValidationId = ValidationId,
                    PackageKey = PackageKey,
                    ValidatorName = nameof(BValidator),
                    State = ValidationStatus.Incomplete,
                };

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentException>(() => _target.AddStatusAsync(validationStatus));
            }

            [Theory]
            [MemberData(nameof(PossibleValidationStatuses))]
            public async Task AddStatusAsyncMethodPersistsStatus(ValidationStatus status)
            {
                // Arrange
                var validatorName = nameof(AValidator);
                var validatorStatuses = new Mock<IDbSet<ValidatorStatus>>();

                _validationContext.Mock(validatorStatusesMock: validatorStatuses);

                // Act & Assert
                var result = await _target.AddStatusAsync(new ValidatorStatus
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
            public TheSaveStatusAsyncMethod(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task SaveStatusAsyncThrowsIfValidatorNameIsWrong()
            {
                // Arrange
                var validatorStatus = new ValidatorStatus
                {
                    ValidationId = ValidationId,
                    PackageKey = PackageKey,
                    ValidatorName = nameof(BValidator),
                    State = ValidationStatus.NotStarted,
                };

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentException>(() => _target.AddStatusAsync(validatorStatus));
            }

            [Theory]
            [MemberData(nameof(PossibleValidationStatuses))]
            public async Task SaveStatusAsyncMethodPersistsStatus(ValidationStatus status)
            {
                // Arrange
                var validatorStatus = new ValidatorStatus
                {
                    ValidationId = ValidationId,
                    PackageKey = PackageKey,
                    ValidatorName = nameof(AValidator),
                    State = status,
                };

                _validationContext.Mock();

                // Act & Assert
                await _target.AddStatusAsync(validatorStatus);

                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);
            }

            public static IEnumerable<object[]> PossibleValidationStatuses => possibleValidationStatuses.Select(s => new object[] { s });
        }

        public class TheTryAddValidatorStatusAsyncMethod : FactsBase
        {
            public TheTryAddValidatorStatusAsyncMethod(ITestOutputHelper output) : base(output)
            {
            }

            private ValidatorStatus NewStatus => new ValidatorStatus
            {
                ValidationId = ValidationId,
                PackageKey = PackageKey,
                ValidatorName = nameof(AValidator),
            };

            private ValidatorStatus NewStatusForUnifiedValidation => new ValidatorStatus
            {
                ValidationId = ValidationId1,
                ValidatorName = nameof(CValidator),
            };

            [Fact]
            public async Task HandlesUniqueConstraintViolationGracefully()
            {
                // Arrange
                var exception = new DbUpdateException(
                    message: "Fail!",
                    innerException: CreateSqlException(2627, "No can do, friend."));
                _validationContext
                    .Setup(x => x.SaveChangesAsync())
                    .ThrowsAsync(exception);

                _validationContext.Mock(validatorStatusesMock: new Mock<IDbSet<ValidatorStatus>>());
                var status = NewStatus;

                // Act
                var result = await _target.TryAddValidatorStatusAsync(
                    _validationRequest.Object,
                    status,
                    ValidationStatus.Succeeded);

                // Assert
                Assert.Same(status, result);
                _validationContext.Verify(x => x.ValidatorStatuses, Times.Exactly(2));
            }

            [Fact]
            public async Task HandlesUniqueConstraintViolationGracefullyForUnifiedRequest()
            {
                // Arrange
                var exception = new DbUpdateException(
                    message: "Fail!",
                    innerException: CreateSqlException(2627, "No can do, friend."));
                _validationContext
                    .Setup(x => x.SaveChangesAsync())
                    .ThrowsAsync(exception);

                _validationContext.Mock(validatorStatusesMock: new Mock<IDbSet<ValidatorStatus>>());
                var status = NewStatusForUnifiedValidation;

                // Act
                var result = await _targetValidatorServiceForUnifiedRequest.TryAddValidatorStatusAsync(
                    _unifiedValidationRequest.Object,
                    status,
                    ValidationStatus.Succeeded);

                // Assert
                Assert.Same(status, result);
                _validationContext.Verify(x => x.ValidatorStatuses, Times.Exactly(2));
            }

            [Fact]
            public async Task PersistsStatus()
            {
                // Arrange
                var validatorName = nameof(AValidator);
                var validatorStatuses = new Mock<IDbSet<ValidatorStatus>>();

                _validationContext.Mock(validatorStatusesMock: validatorStatuses);

                // Act & Assert
                var result = await _target.TryAddValidatorStatusAsync(
                                            _validationRequest.Object,
                                            NewStatus,
                                            ValidationStatus.NotStarted);

                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);

                validatorStatuses
                    .Verify(statuses => statuses.Add(
                        It.Is<ValidatorStatus>(
                            s => s.ValidationId == ValidationId
                                && s.PackageKey == PackageKey
                                && s.ValidatorName == validatorName
                                && s.State == ValidationStatus.NotStarted)),
                    Times.Once);

                Assert.Equal(ValidationStatus.NotStarted, result.State);
            }

            [Fact]
            public async Task PersistsStatusForUnifiedValidationRequest()
            {
                // Arrange
                var validatorName = nameof(CValidator);
                var validatorStatuses = new Mock<IDbSet<ValidatorStatus>>();

                _validationContext.Mock(validatorStatusesMock: validatorStatuses);

                // Act & Assert
                var result = await _targetValidatorServiceForUnifiedRequest.TryAddValidatorStatusAsync(
                                            _unifiedValidationRequest.Object,
                                            NewStatusForUnifiedValidation,
                                            ValidationStatus.NotStarted);

                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);

                validatorStatuses
                    .Verify(statuses => statuses.Add(
                        It.Is<ValidatorStatus>(
                            s => s.ValidationId == ValidationId1
                                && s.ValidatorName == validatorName
                                && s.State == ValidationStatus.NotStarted)),
                    Times.Once);

                Assert.Equal(ValidationStatus.NotStarted, result.State);
            }

        }

        public class TheTryUpdateValidationStatusAsyncMethod : FactsBase
        {
            public TheTryUpdateValidationStatusAsyncMethod(ITestOutputHelper output) : base(output)
            {
            }

            private ValidatorStatus ExistingStatus => new ValidatorStatus
            {
                ValidationId = ValidationId,
                PackageKey = PackageKey,
                ValidatorName = nameof(AValidator),
                State = ValidationStatus.NotStarted,
            };

            private ValidatorStatus ExistingStatusForUnifiedValidation => new ValidatorStatus
            {
                ValidationId = ValidationId1,
                ValidatorName = nameof(CValidator),
                State = ValidationStatus.NotStarted,
            };

            [Fact]
            public async Task HandlesUniqueConstraintViolationGracefully()
            {
                // Arrange
                var exception = new DbUpdateConcurrencyException("Fail!");
                _validationContext
                    .Setup(x => x.SaveChangesAsync())
                    .ThrowsAsync(exception);

                _validationContext.Mock(validatorStatusesMock: new Mock<IDbSet<ValidatorStatus>>());
                var status = ExistingStatus;
                _validationContext.Object.ValidatorStatuses.Add(status);

                // Act
                var result = await _target.TryUpdateValidationStatusAsync(
                    _validationRequest.Object,
                    status,
                    ValidationStatus.Succeeded);

                // Assert
                Assert.Same(status, result);
                _validationContext.Verify(x => x.ValidatorStatuses, Times.Exactly(2));
            }

            [Fact]
            public async Task HandlesUniqueConstraintViolationGracefullyForUnifiedValidation()
            {
                // Arrange
                var exception = new DbUpdateConcurrencyException("Fail!");
                _validationContext
                    .Setup(x => x.SaveChangesAsync())
                    .ThrowsAsync(exception);

                _validationContext.Mock(validatorStatusesMock: new Mock<IDbSet<ValidatorStatus>>());
                var status = ExistingStatusForUnifiedValidation;
                _validationContext.Object.ValidatorStatuses.Add(status);

                // Act
                var result = await _targetValidatorServiceForUnifiedRequest.TryUpdateValidationStatusAsync(
                    _unifiedValidationRequest.Object,
                    status,
                    ValidationStatus.Succeeded);

                // Assert
                Assert.Same(status, result);
                _validationContext.Verify(x => x.ValidatorStatuses, Times.Exactly(2));
            }

            [Fact]
            public async Task PersistsStatus()
            {
                // Arrange
                var existingStatus = ExistingStatus;

                _validationContext.Mock(validatorStatuses: new[] { existingStatus });

                // Act & Assert
                var result = await _target.TryUpdateValidationStatusAsync(
                                            _validationRequest.Object,
                                            existingStatus,
                                            ValidationStatus.Succeeded);

                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);

                Assert.Equal(ValidationStatus.Succeeded, existingStatus.State);
                Assert.Same(existingStatus, result);
            }

            [Fact]
            public async Task PersistsStatusForUnifiedValidation()
            {
                // Arrange
                var existingStatus = ExistingStatusForUnifiedValidation;

                _validationContext.Mock(validatorStatuses: new[] { existingStatus });

                // Act & Assert
                var result = await _targetValidatorServiceForUnifiedRequest.TryUpdateValidationStatusAsync(
                                            _unifiedValidationRequest.Object,
                                            existingStatus,
                                            ValidationStatus.Succeeded);

                _validationContext.Verify(c => c.SaveChangesAsync(), Times.Once);

                Assert.Equal(ValidationStatus.Succeeded, existingStatus.State);
                Assert.Same(existingStatus, result);
            }

        }

        public abstract class FactsBase
        {
            protected readonly ITestOutputHelper _output;
            protected readonly Mock<IValidationEntitiesContext> _validationContext;
            protected readonly ILogger<ValidatorStateService> _logger;
            protected readonly Mock<INuGetValidationRequest> _validationRequest;
            protected readonly Mock<IValidationRequest> _unifiedValidationRequest;
            protected readonly ValidatorStateService _target;
            protected readonly ValidatorStateService _targetValidatorServiceForUnifiedRequest;

            public FactsBase(ITestOutputHelper output)
            {
                _output = output ?? throw new ArgumentNullException(nameof(output));
                _validationContext = new Mock<IValidationEntitiesContext>();
                _logger = new LoggerFactory().AddXunit(_output).CreateLogger<ValidatorStateService>();

                _validationRequest = new Mock<INuGetValidationRequest>();
                _validationRequest.Setup(x => x.NupkgUrl).Returns(NupkgUrl);
                _validationRequest.Setup(x => x.PackageId).Returns(PackageId);
                _validationRequest.Setup(x => x.PackageKey).Returns(PackageKey);
                _validationRequest.Setup(x => x.PackageVersion).Returns(PackageVersion);
                _validationRequest.Setup(x => x.ValidationId).Returns(ValidationId);

                _unifiedValidationRequest = new Mock<IValidationRequest>();
                _unifiedValidationRequest.Setup(x => x.InputUrl).Returns(InputUrl);
                _unifiedValidationRequest.Setup(x => x.ValidationStepId).Returns(ValidationId1);

                _target = CreateValidatorStateService(ValidatorUtility.GetValidatorName(typeof(AValidator)));
                _targetValidatorServiceForUnifiedRequest = CreateValidatorStateService(ValidatorUtility.GetValidatorName(typeof(CValidator)));
            }

            protected ValidatorStateService CreateValidatorStateService(string validatorName)
                => new ValidatorStateService(
                    _validationContext.Object,
                    validatorName,
                    _logger);

            /// <summary>
            /// Source: http://blog.gauffin.org/2014/08/how-to-create-a-sqlexception/
            /// </summary>
            protected SqlException CreateSqlException(int number, string message)
            {
                var collectionConstructor = typeof(SqlErrorCollection)
                    .GetConstructor(
                        bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance,
                        binder: null,
                        types: new Type[0],
                        modifiers: null);
                var addMethod = typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance);
                var errorCollection = (SqlErrorCollection)collectionConstructor.Invoke(null);

                var errorConstructor = typeof(SqlError).GetConstructor(
                    bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance,
                    binder: null,
                    types: new[]
                    {
                        typeof (int), typeof (byte), typeof (byte), typeof (string), typeof(string), typeof (string),
                        typeof (int), typeof (uint)
                    },
                    modifiers: null);
                var error = errorConstructor.Invoke(new object[]
                {
                    number,
                    (byte)0,
                    (byte)0,
                    "server",
                    "errMsg",
                    "procedure",
                    100,
                    (uint)0
                });

                addMethod.Invoke(errorCollection, new[] { error });

                var constructor = typeof(SqlException).GetConstructor(
                    bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance,
                    binder: null,
                    types: new[] { typeof(string), typeof(SqlErrorCollection), typeof(Exception), typeof(Guid) },
                    modifiers: null);
                return (SqlException)constructor.Invoke(new object[] { message, errorCollection, null, Guid.NewGuid() });
            }
        }
    }
}
