// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.PackageSigning.ExtractAndValidateSignature;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.Services.Validation;
using Xunit;

namespace Validation.PackageSigning.ExtractAndValidateSignature.Tests
{
    public class SignatureValidationMessageHandlerFacts
    {
        private static readonly Uri TestPackageUri = new Uri("http://example/validation/NuGet.Versioning.4.3.0.nupkg");

        public class HandleAsync
        {
            private readonly SignatureValidationMessage _message;
            private readonly ValidatorStatus _validation;
            private readonly Mock<IValidationIssue> _validationIssue;
            private readonly SignatureValidatorResult _validatorResult;
            private readonly Mock<IPackageDownloader> _packageDownloader;
            private readonly Mock<IValidatorStateService> _validatorStateService;
            private readonly Mock<ISignatureValidator> _signatureValidator;
            private readonly Mock<ILogger<SignatureValidationMessageHandler>> _logger;
            private readonly SignatureValidationMessageHandler _target;

            public HandleAsync()
            {
                _message = new SignatureValidationMessage(
                    "NuGet.Versioning",
                    "4.3.0",
                    TestPackageUri,
                    new Guid("18e83aca-953a-4484-a698-a8fb8619e0bd"));

                _validation = new ValidatorStatus
                {
                    PackageKey = 42,
                    State = ValidationStatus.Incomplete,
                };
                _validationIssue = new Mock<IValidationIssue>();
                _validatorResult = new SignatureValidatorResult(ValidationStatus.Succeeded);

                _packageDownloader = new Mock<IPackageDownloader>();
                _validatorStateService = new Mock<IValidatorStateService>();
                _signatureValidator = new Mock<ISignatureValidator>();
                _logger = new Mock<ILogger<SignatureValidationMessageHandler>>();

                _packageDownloader
                    .Setup(x => x.DownloadAsync(_message.NupkgUri, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => TestResources.GetResourceStream(TestResources.UnsignedPackage));
                _validatorStateService
                    .Setup(x => x.GetStatusAsync(It.IsAny<Guid>()))
                    .ReturnsAsync(() => _validation);

                _signatureValidator
                    .Setup(x => x.ValidateAsync(
                        It.IsAny<int>(),
                        It.IsAny<ISignedPackage>(),
                        It.IsAny<SignatureValidationMessage>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => _validatorResult)
                    .Callback(() => _validation.State = ValidationStatus.Succeeded);

                _target = new SignatureValidationMessageHandler(
                    _packageDownloader.Object,
                    _validatorStateService.Object,
                    _signatureValidator.Object,
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
                        It.IsAny<ISignedPackage>(),
                        It.IsAny<SignatureValidationMessage>(),
                        It.IsAny<CancellationToken>()),
                    Times.Never);
                _validatorStateService.Verify(
                    x => x.SaveStatusAsync(It.IsAny<ValidatorStatus>()),
                    Times.Never);
            }

            [Theory]
            [InlineData(TestResources.UnsignedPackage, "TestUnsigned", "1.0.0")]
            [InlineData(TestResources.SignedPackageLeaf1, "TestSigned.leaf-1", "1.0.0")]
            [InlineData(TestResources.SignedPackageLeaf2, "TestSigned.leaf-2", "2.0.0")]
            public async Task LoadsTheDownloadPackage(string resourceName, string id, string version)
            {
                // Arrange
                string validatedId = null;
                string validatedVersion = null;
                _packageDownloader
                    .Setup(x => x.DownloadAsync(_message.NupkgUri, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => TestResources.GetResourceStream(resourceName));
                _signatureValidator
                    .Setup(x => x.ValidateAsync(
                        It.IsAny<int>(),
                        It.IsAny<ISignedPackage>(),
                        It.IsAny<SignatureValidationMessage>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(_validatorResult)
                    .Callback<int, ISignedPackageReader, SignatureValidationMessage, CancellationToken>((_, v, __, ___) =>
                    {
                        _validation.State = ValidationStatus.Succeeded;
                        var identity = ((IPackageCoreReader)v).GetIdentity();
                        validatedId = identity.Id;
                        validatedVersion = identity.Version.ToNormalizedString();
                    });

                // Act
                var success = await _target.HandleAsync(_message);

                // Assert
                Assert.True(success, "The handler should have succeeded processing the message.");
                Assert.Equal(id, validatedId);
                Assert.Equal(version, validatedVersion);
            }

            [Theory]
            [InlineData(ValidationStatus.Failed)]
            [InlineData(ValidationStatus.Succeeded)]
            public async Task SavesStateWithIssuesWhenTerminal(ValidationStatus state)
            {
                // Arrange & Act
                bool success = await SetupUpSavesState(state, new[] { _validationIssue.Object });

                // Assert
                Assert.True(success, "The handler should have succeeded processing the message.");
                Assert.Equal(state, _validation.State);
                var issue = Assert.Single(_validation.ValidatorIssues);
                Assert.Equal(ValidationIssueCode.PackageIsSigned, issue.IssueCode);
                Assert.Equal("serialized...", issue.Data);
                _validatorStateService.Verify(
                    x => x.SaveStatusAsync(It.IsAny<ValidatorStatus>()),
                    Times.Once);
            }

            [Theory]
            [InlineData(ValidationStatus.NotStarted)]
            [InlineData(ValidationStatus.Incomplete)]
            public async Task SavesStateWithoutIssuesWhenNonTerminal(ValidationStatus state)
            {
                // Arrange & Act
                bool success = await SetupUpSavesState(state, new IValidationIssue[0]);

                // Assert
                Assert.False(success, "The handler should have failed processing the message.");
                Assert.Equal(state, _validation.State);
                Assert.Null(_validation.ValidatorIssues);
                _validatorStateService.Verify(
                    x => x.SaveStatusAsync(It.IsAny<ValidatorStatus>()),
                    Times.Never);
            }

            private async Task<bool> SetupUpSavesState(ValidationStatus state, IReadOnlyList<IValidationIssue> issues)
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
                        It.IsAny<ISignedPackage>(),
                        It.IsAny<SignatureValidationMessage>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new SignatureValidatorResult(state, issues));

                // Act
                var success = await _target.HandleAsync(_message);
                return success;
            }
        }
    }
}
