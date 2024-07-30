// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.ProcessSignature;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Validation;
using Xunit;

namespace Validation.PackageSigning.ProcessSignature.Tests
{
    public class SignatureValidationMessageHandlerFacts
    {
        private static readonly Uri TestPackageUri = new Uri("http://example/validation/NuGet.Versioning.4.3.0.nupkg");

        public class HandleAsync
        {
            private readonly SignatureValidationMessage _message;
            private readonly Uri _outputNupkgUri;
            private readonly ValidatorStatus _validation;
            private readonly Mock<IValidationIssue> _validationIssue;
            private SignatureValidatorResult _validatorResult;
            private readonly Mock<IFileDownloader> _packageDownloader;
            private readonly Mock<IValidatorStateService> _validatorStateService;
            private readonly Mock<ISignatureValidator> _signatureValidator;
            private readonly Mock<IPackageValidationEnqueuer> _validationEnqueuer;
            private readonly Mock<IFeatureFlagService> _featureFlagService;
            private readonly Mock<ILogger<SignatureValidationMessageHandler>> _logger;
            private readonly SignatureValidationMessageHandler _target;

            public HandleAsync()
            {
                _message = new SignatureValidationMessage(
                    "NuGet.Versioning",
                    "4.3.0",
                    TestPackageUri,
                    new Guid("18e83aca-953a-4484-a698-a8fb8619e0bd"));
                _outputNupkgUri = new Uri("https://example/processor/18e83aca-953a-4484-a698-a8fb8619e0bd/nuget.versioning.4.3.0.nupkg");

                _validation = new ValidatorStatus
                {
                    PackageKey = 42,
                    State = ValidationStatus.Incomplete,
                };
                _validationIssue = new Mock<IValidationIssue>();
                _validatorResult = new SignatureValidatorResult(ValidationStatus.Succeeded, nupkgUri: null);

                _packageDownloader = new Mock<IFileDownloader>();
                _validatorStateService = new Mock<IValidatorStateService>();
                _signatureValidator = new Mock<ISignatureValidator>();
                _validationEnqueuer = new Mock<IPackageValidationEnqueuer>();
                _featureFlagService = new Mock<IFeatureFlagService>();
                _logger = new Mock<ILogger<SignatureValidationMessageHandler>>();

                _packageDownloader
                    .Setup(x => x.DownloadAsync(_message.NupkgUri, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => FileDownloadResult.Ok(TestResources.GetResourceStream(TestResources.UnsignedPackage)));
                _validatorStateService
                    .Setup(x => x.GetStatusAsync(It.IsAny<Guid>()))
                    .ReturnsAsync(() => _validation);

                _signatureValidator
                    .Setup(x => x.ValidateAsync(
                        It.IsAny<int>(),
                        It.IsAny<Stream>(),
                        It.IsAny<SignatureValidationMessage>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => _validatorResult);

                _featureFlagService.SetReturnsDefault(true);

                _target = new SignatureValidationMessageHandler(
                    _packageDownloader.Object,
                    _validatorStateService.Object,
                    _signatureValidator.Object,
                    _validationEnqueuer.Object,
                    _featureFlagService.Object,
                    _logger.Object);
            }

            [Theory]
            [InlineData(ValidationStatus.NotStarted, false)]
            [InlineData(ValidationStatus.Failed, true)]
            [InlineData(ValidationStatus.Succeeded, true)]
            public async Task HandlesUnexpectedPackageStatuses(ValidationStatus state, bool expectedSuccess)
            {
                // Arrange
                _validation.State = state;

                // Act
                var actualSuccess = await _target.HandleAsync(_message);

                // Assert
                Assert.Equal(expectedSuccess, actualSuccess);
                _validatorStateService.Verify(
                    x => x.GetStatusAsync(It.IsAny<Guid>()),
                    Times.Once);
                _signatureValidator.Verify(
                    x => x.ValidateAsync(
                        It.IsAny<int>(),
                        It.IsAny<Stream>(),
                        It.IsAny<SignatureValidationMessage>(),
                        It.IsAny<CancellationToken>()),
                    Times.Never);
                _validatorStateService.Verify(
                    x => x.SaveStatusAsync(It.IsAny<ValidatorStatus>()),
                    Times.Never);
            }

            [Fact]
            public async Task SetsNupkgUrlIfValidationSucceeds()
            {
                // Arrange
                _validatorResult = new SignatureValidatorResult(ValidationStatus.Succeeded, _outputNupkgUri);

                // Act
                var success = await _target.HandleAsync(_message);

                // Assert
                Assert.True(success, "The handler should have succeeded processing the message.");
                Assert.Equal(_outputNupkgUri.AbsoluteUri, _validation.NupkgUrl);
            }

            [Fact]
            public async Task VerifiesTheDownloadedStream()
            {
                // Arrange
                var stream = TestResources.GetResourceStream(TestResources.SignedPackageLeaf1);
                _packageDownloader
                    .Setup(x => x.DownloadAsync(_message.NupkgUri, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => FileDownloadResult.Ok(stream));

                // Act
                var success = await _target.HandleAsync(_message);

                // Assert
                Assert.True(success, "The handler should have succeeded processing the message.");
                _signatureValidator.Verify(
                    x => x.ValidateAsync(
                        _validation.PackageKey,
                        stream,
                        _message,
                        CancellationToken.None),
                    Times.Once);
                _signatureValidator.Verify(
                    x => x.ValidateAsync(
                        It.IsAny<int>(),
                        It.IsAny<Stream>(),
                        It.IsAny<SignatureValidationMessage>(),
                        It.IsAny<CancellationToken>()),
                    Times.Once);
            }

            [Theory]
            [InlineData(ValidationStatus.Failed)]
            [InlineData(ValidationStatus.Succeeded)]
            public async Task SavesStateWithIssuesWhenTerminal(ValidationStatus state)
            {
                // Arrange & Act
                bool success = await SetupState(state, new[] { _validationIssue.Object });

                // Assert
                Assert.True(success, "The handler should have succeeded processing the message.");
                Assert.Equal(state, _validation.State);
                var issue = Assert.Single(_validation.ValidatorIssues);
                Assert.Equal(ValidationIssueCode.PackageIsSigned, issue.IssueCode);
                Assert.Equal("serialized...", issue.Data);
                _validatorStateService.Verify(
                    x => x.SaveStatusAsync(It.IsAny<ValidatorStatus>()),
                    Times.Once);
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
                _featureFlagService.Setup(x => x.IsQueueBackEnabled()).Returns(false);
                
                // Act
                bool success = await SetupState(ValidationStatus.Succeeded, new[] { _validationIssue.Object });

                // Assert
                Assert.True(success, "The handler should have succeeded processing the message.");
                Assert.Equal(ValidationStatus.Succeeded, _validation.State);
                var issue = Assert.Single(_validation.ValidatorIssues);
                Assert.Equal(ValidationIssueCode.PackageIsSigned, issue.IssueCode);
                Assert.Equal("serialized...", issue.Data);
                _validatorStateService.Verify(
                    x => x.SaveStatusAsync(It.IsAny<ValidatorStatus>()),
                    Times.Once);
                _validationEnqueuer.Verify(
                    x => x.SendMessageAsync(It.IsAny<PackageValidationMessageData>()),
                    Times.Never);
            }

            [Theory]
            [InlineData(ValidationStatus.NotStarted)]
            [InlineData(ValidationStatus.Incomplete)]
            public async Task SavesStateWithoutIssuesWhenNonTerminal(ValidationStatus state)
            {
                // Arrange & Act
                bool success = await SetupState(state, new IValidationIssue[0]);

                // Assert
                Assert.False(success, "The handler should have failed processing the message.");
                Assert.Equal(state, _validation.State);
                Assert.Null(_validation.ValidatorIssues);
                _validatorStateService.Verify(
                    x => x.SaveStatusAsync(It.IsAny<ValidatorStatus>()),
                    Times.Never);
                _validationEnqueuer.Verify(
                    x => x.SendMessageAsync(It.IsAny<PackageValidationMessageData>()),
                    Times.Never);
            }

            private async Task<bool> SetupState(ValidationStatus state, IReadOnlyList<IValidationIssue> issues)
            {
                // Arrange
                _validationIssue
                    .Setup(x => x.IssueCode)
                    .Returns(ValidationIssueCode.PackageIsSigned);
                _validationIssue
                    .Setup(x => x.Serialize())
                    .Returns("serialized...");
                _signatureValidator
                    .Setup(x => x.ValidateAsync(
                        It.IsAny<int>(),
                        It.IsAny<Stream>(),
                        It.IsAny<SignatureValidationMessage>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new SignatureValidatorResult(state, issues, nupkgUri: null));

                // Act
                var success = await _target.HandleAsync(_message);
                return success;
            }
        }
    }
}
