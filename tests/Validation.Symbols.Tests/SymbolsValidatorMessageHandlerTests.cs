// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation.Storage;
using NuGet.Jobs.Validation.Symbols.Core;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Issues;
using Moq;
using Xunit;
using NuGet.Jobs.Validation;

namespace Validation.Symbols.Tests
{
    public class SymbolsValidatorMessageHandlerTests
    {
        public class TheHandleAsyncMethod : FactBase
        {
            [Fact]
            public void MessageNullCheck()
            {
                // Act + Assert
                Assert.ThrowsAsync<ArgumentNullException>(() => Target.HandleAsync(null));
            }

            [Fact]
            public async Task ReturnsFalseWhenTheValidatorStateIsNotSaved()
            {
                // Arrange 
                ValidatorStatus status = null;
                _validatorStateService.Setup(s => s.GetStatusAsync(It.IsAny<Guid>())).ReturnsAsync(status);

                // Act 
                var result = await Target.HandleAsync(_message);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public async Task ReturnsFalseWhenTheValidatorStateIsNotStarted()
            {
                // Arrange 
                ValidatorStatus status = new ValidatorStatus()
                { State = ValidationStatus.NotStarted };

                _validatorStateService.Setup(s => s.GetStatusAsync(It.IsAny<Guid>())).ReturnsAsync(status);

                // Act 
                var result = await Target.HandleAsync(_message);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public async Task ReturnsTrueWhenTheValidatorStateIsSucceded()
            {
                // Arrange 
                ValidatorStatus status = new ValidatorStatus()
                { State = ValidationStatus.Succeeded };

                _validatorStateService.Setup(s => s.GetStatusAsync(It.IsAny<Guid>())).ReturnsAsync(status);

                // Act 
                var result = await Target.HandleAsync(_message);

                // Assert
                Assert.True(result);
                _validationEnqueuer.Verify(
                    x => x.SendMessageAsync(It.IsAny<PackageValidationMessageData>()),
                    Times.Never);
            }

            [Fact]
            public async Task ReturnsTrueWhenTheValidatorStateIsFailed()
            {
                // Arrange 
                ValidatorStatus status = new ValidatorStatus()
                { State = ValidationStatus.Failed };

                _validatorStateService.Setup(s => s.GetStatusAsync(It.IsAny<Guid>())).ReturnsAsync(status);

                // Act 
                var result = await Target.HandleAsync(_message);

                // Assert
                Assert.True(result);
                _validationEnqueuer.Verify(
                    x => x.SendMessageAsync(It.IsAny<PackageValidationMessageData>()),
                    Times.Never);
            }

            [Fact]
            public async Task IncompleteStateIsProcessed()
            {
                // Arrange 
                ValidatorStatus status = new ValidatorStatus()
                { State = ValidationStatus.Incomplete };

                _validatorStateService.Setup(s => s.GetStatusAsync(It.IsAny<Guid>())).ReturnsAsync(status);
                _symbolService.Setup(s => s.ValidateSymbolsAsync(It.IsAny<SymbolsValidatorMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(ValidationResult.Incomplete);

                // Act 
                var result = await Target.HandleAsync(_message);

                // Assert
                Assert.False(result);
                _symbolService.Verify(ss => ss.ValidateSymbolsAsync(It.IsAny<SymbolsValidatorMessage>(), It.IsAny<CancellationToken>()), Times.Once);
            }

            [Fact]
            public async Task IncompleteStateIsProcessedAndSavedOnSuccess()
            {
                // Arrange 
                ValidatorStatus status = new ValidatorStatus()
                { State = ValidationStatus.Incomplete };

                _validatorStateService.Setup(s => s.GetStatusAsync(It.IsAny<Guid>())).ReturnsAsync(status);
                _validatorStateService.Setup(s => s.SaveStatusAsync(It.IsAny<ValidatorStatus>())).ReturnsAsync(SaveStatusResult.Success);
                _symbolService.Setup(s => s.ValidateSymbolsAsync(It.IsAny<SymbolsValidatorMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(ValidationResult.Succeeded);

                // Act 
                var result = await Target.HandleAsync(_message);

                // Assert
                Assert.True(result);
                _validatorStateService.Verify(ss => ss.SaveStatusAsync(It.IsAny<ValidatorStatus>()), Times.Once);
                _validationEnqueuer.Verify(
                    x => x.SendMessageAsync(It.IsAny<PackageValidationMessageData>()),
                    Times.Once);
                _validationEnqueuer.Verify(
                    x => x.SendMessageAsync(It.Is<PackageValidationMessageData>(d => d.Type == PackageValidationMessageType.CheckValidator)),
                    Times.Once);
            }

            [Fact]
            public async Task DoesNotEnqueueIfFeatureFlagIsOff()
            {
                // Arrange 
                ValidatorStatus status = new ValidatorStatus() { State = ValidationStatus.Incomplete };

                _validatorStateService.Setup(s => s.GetStatusAsync(It.IsAny<Guid>())).ReturnsAsync(status);
                _validatorStateService.Setup(s => s.SaveStatusAsync(It.IsAny<ValidatorStatus>())).ReturnsAsync(SaveStatusResult.Success);
                _symbolService.Setup(s => s.ValidateSymbolsAsync(It.IsAny<SymbolsValidatorMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(ValidationResult.Succeeded);
                _featureFlagService.Setup(x => x.IsQueueBackEnabled()).Returns(false);

                // Act 
                var result = await Target.HandleAsync(_message);

                // Assert
                Assert.True(result);
                _validatorStateService.Verify(ss => ss.SaveStatusAsync(It.IsAny<ValidatorStatus>()), Times.Once);
                _validationEnqueuer.Verify(
                    x => x.SendMessageAsync(It.IsAny<PackageValidationMessageData>()),
                    Times.Never);
            }

            [Fact]
            public async Task IncompleteStateIsProcessedAndSavedOnFailed()
            {
                // Arrange 
                ValidatorStatus status = new ValidatorStatus()
                { State = ValidationStatus.Incomplete };

                _validatorStateService.Setup(s => s.GetStatusAsync(It.IsAny<Guid>())).ReturnsAsync(status);
                _validatorStateService.Setup(s => s.SaveStatusAsync(It.IsAny<ValidatorStatus>())).ReturnsAsync(SaveStatusResult.Success);

                _symbolService.Setup(s => s.ValidateSymbolsAsync(It.IsAny<SymbolsValidatorMessage>(), It.IsAny<CancellationToken>())).
                    ReturnsAsync(ValidationResult.FailedWithIssues(ValidationIssue.Unknown));

                // Act 
                var result = await Target.HandleAsync(_message);

                // Assert
                Assert.True(result);
                Assert.Equal(1, status.ValidatorIssues.Count);
                _validatorStateService.Verify(ss => ss.SaveStatusAsync(It.IsAny<ValidatorStatus>()), Times.Once);
            }

            [Fact]
            public async Task ReturnsFalseIfFailToSaveInDB()
            {
                // Arrange 
                ValidatorStatus status = new ValidatorStatus()
                { State = ValidationStatus.Incomplete };

                _validatorStateService.Setup(s => s.GetStatusAsync(It.IsAny<Guid>())).ReturnsAsync(status);
                _validatorStateService.Setup(s => s.SaveStatusAsync(It.IsAny<ValidatorStatus>())).ReturnsAsync(SaveStatusResult.StaleStatus);

                _symbolService.Setup(s => s.ValidateSymbolsAsync(It.IsAny<SymbolsValidatorMessage>(), It.IsAny<CancellationToken>())).
                    ReturnsAsync(ValidationResult.Succeeded);

                // Act 
                var result = await Target.HandleAsync(_message);

                // Assert
                Assert.False(result);
            }
        }

        public class FactBase
        {
            public Mock<ISymbolsValidatorService> _symbolService;
            public Mock<IValidatorStateService> _validatorStateService;
            public Mock<IPackageValidationEnqueuer> _validationEnqueuer;
            public Mock<IFeatureFlagService> _featureFlagService;
            public Mock<ILogger<SymbolsValidatorMessageHandler>> _logger;

            public SymbolsValidatorMessage _message;
            public ValidatorStatus _status;

            public FactBase()
            {
                _symbolService = new Mock<ISymbolsValidatorService>();
                _validatorStateService = new Mock<IValidatorStateService>();
                _validationEnqueuer = new Mock<IPackageValidationEnqueuer>();
                _featureFlagService = new Mock<IFeatureFlagService>();
                _logger = new Mock<ILogger<SymbolsValidatorMessageHandler>>();

                _featureFlagService.SetReturnsDefault(true);

                Target = new SymbolsValidatorMessageHandler(
                    _symbolService.Object,
                    _validatorStateService.Object,
                    _validationEnqueuer.Object,
                    _featureFlagService.Object,
                    _logger.Object);

                _message = new SymbolsValidatorMessage(Guid.NewGuid(), 42, "TestPackage", "1.1.1", "url");
            }

            public SymbolsValidatorMessageHandler Target { get; }
        }
    }
}
