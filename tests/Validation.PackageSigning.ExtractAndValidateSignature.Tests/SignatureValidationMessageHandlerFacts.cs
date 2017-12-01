// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
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
            private readonly Dictionary<Uri, string> _urlToResourceName;
            private readonly EmbeddedResourceTestHandler _handler;
            private readonly HttpClient _httpClient;
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
                _urlToResourceName = new Dictionary<Uri, string>
                {
                    { _message.NupkgUri, TestResources.UnsignedPackage },
                };

                _handler = new EmbeddedResourceTestHandler(_urlToResourceName);
                _httpClient = new HttpClient(_handler);
                _validatorStateService = new Mock<IValidatorStateService>();
                _signatureValidator = new Mock<ISignatureValidator>();
                _logger = new Mock<ILogger<SignatureValidationMessageHandler>>();

                _validatorStateService
                    .Setup(x => x.GetStatusAsync(It.IsAny<Guid>()))
                    .ReturnsAsync(() => _validation);

                _signatureValidator
                    .Setup(x => x.ValidateAsync(
                        It.IsAny<ISignedPackageReader>(),
                        It.IsAny<ValidatorStatus>(),
                        It.IsAny<SignatureValidationMessage>(),
                        It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask)
                    .Callback(() => _validation.State = ValidationStatus.Succeeded);

                _target = new SignatureValidationMessageHandler(
                    _httpClient,
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
                        It.IsAny<ISignedPackageReader>(),
                        It.IsAny<ValidatorStatus>(),
                        It.IsAny<SignatureValidationMessage>(),
                        It.IsAny<CancellationToken>()),
                    Times.Never);
                _validatorStateService.Verify(
                    x => x.SaveStatusAsync(It.IsAny<ValidatorStatus>()),
                    Times.Never);
            }

            [Theory]
            [InlineData(TestResources.UnsignedPackage, "TestUnsigned", "1.0.0")]
            [InlineData(TestResources.SignedPackage1, "TestSigned.leaf-1", "1.0.0")]
            [InlineData(TestResources.SignedPackage2, "TestSigned.leaf-2", "2.0.0")]
            public async Task LoadsTheDownloadPackage(string resourceName, string id, string version)
            {
                // Arrange
                string validatedId = null;
                string validatedVersion = null;
                _urlToResourceName[_message.NupkgUri] = resourceName;
                _signatureValidator
                    .Setup(x => x.ValidateAsync(
                        It.IsAny<ISignedPackageReader>(),
                        It.IsAny<ValidatorStatus>(),
                        It.IsAny<SignatureValidationMessage>(),
                        It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask)
                    .Callback<ISignedPackageReader, ValidatorStatus, SignatureValidationMessage, CancellationToken>((v, _, __, ___) =>
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
            public async Task SavesTerminalState(ValidationStatus state)
            {
                // Arrange
                _signatureValidator
                    .Setup(x => x.ValidateAsync(
                        It.IsAny<ISignedPackageReader>(),
                        It.IsAny<ValidatorStatus>(),
                        It.IsAny<SignatureValidationMessage>(),
                        It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask)
                    .Callback(() => _validation.State = state);

                // Act
                var success = await _target.HandleAsync(_message);

                // Assert
                Assert.True(success, "The handler should have succeeded processing the message.");
                _validatorStateService.Verify(
                    x => x.SaveStatusAsync(It.IsAny<ValidatorStatus>()),
                    Times.Once);
            }

            [Theory]
            [InlineData(ValidationStatus.NotStarted)]
            [InlineData(ValidationStatus.Incomplete)]
            public async Task RejectsNonTerminalState(ValidationStatus state)
            {
                // Arrange
                _signatureValidator
                    .Setup(x => x.ValidateAsync(
                        It.IsAny<ISignedPackageReader>(),
                        It.IsAny<ValidatorStatus>(),
                        It.IsAny<SignatureValidationMessage>(),
                        It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask)
                    .Callback(() => _validation.State = state);

                // Act
                var success = await _target.HandleAsync(_message);

                // Assert
                Assert.False(success, "The handler should have failed processing the message.");
                _validatorStateService.Verify(
                    x => x.SaveStatusAsync(It.IsAny<ValidatorStatus>()),
                    Times.Never);
            }
        }
    }
}
