// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation.PackageSigning.ExtractAndValidateSignature;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Services.Validation;
using Xunit;

namespace Validation.PackageSigning.ExtractAndValidateSignature.Tests
{
    public class SignatureValidationMessageHandlerFacts
    {
        private const string ResourceNamespace = "Validation.PackageSigning.ExtractAndValidateSignature.Tests.TestData";
        private const string TestBaseUrl = "http://example/validation/";
        
        public class HandleAsync
        {
            private readonly SignatureValidationMessage _unsignedPackage;
            private readonly SignatureValidationMessage _signedPackage1;
            private readonly SignatureValidationMessage _signedPackage2;
            private readonly ValidatorStatus _validation;
            private readonly Dictionary<Uri, string> _urlToResourceName;
            private readonly EmbeddedResourceTestHandler _handler;
            private readonly HttpClient _httpClient;
            private readonly Mock<IValidatorStateService> _validatorStateService;
            private readonly Mock<IPackageSigningStateService> _packageSigningStateService;
            private readonly Mock<ILogger<SignatureValidationMessageHandler>> _logger;
            private readonly SignatureValidationMessageHandler _target;

            public HandleAsync()
            {
                _unsignedPackage = new SignatureValidationMessage(
                    "TestUnsigned",
                    "1.0.0",
                    new Uri(TestBaseUrl + "TestUnsigned.1.0.0.nupkg"),
                    new Guid("18e83aca-953a-4484-a698-a8fb8619e0bd"));
                _signedPackage1 = new SignatureValidationMessage(
                    "TestSigned.leaf-1",
                    "1.0.0",
                    new Uri(TestBaseUrl + "TestSigned.leaf-1.1.0.0.nupkg"),
                    new Guid("ee3c0cbe-44fe-48b4-9153-663cce2ee5ad"));
                _signedPackage2 = new SignatureValidationMessage(
                    "TestSigned.leaf-2",
                    "2.0.0",
                    new Uri(TestBaseUrl + "TestSigned.leaf-2.2.0.0.nupkg"),
                    new Guid("0c7054dd-9205-4c3a-be9f-8d33eade1e5c"));

                _validation = new ValidatorStatus
                {
                    PackageKey = 42,
                    State = ValidationStatus.Incomplete,
                };
                _urlToResourceName = new Dictionary<Uri, string>
                {
                    { _unsignedPackage.NupkgUri, ResourceNamespace + ".TestUnsigned.1.0.0.nupkg" },
                    { _signedPackage1.NupkgUri, ResourceNamespace + ".TestSigned.leaf-1.1.0.0.nupkg" },
                    { _signedPackage2.NupkgUri, ResourceNamespace + ".TestSigned.leaf-2.1.0.0.nupkg" },
                };

                _handler = new EmbeddedResourceTestHandler(_urlToResourceName);
                _httpClient = new HttpClient(_handler);
                _validatorStateService = new Mock<IValidatorStateService>();
                _packageSigningStateService = new Mock<IPackageSigningStateService>();
                _logger = new Mock<ILogger<SignatureValidationMessageHandler>>();

                _validatorStateService
                    .Setup(x => x.GetStatusAsync(It.IsAny<Guid>()))
                    .ReturnsAsync(() => _validation);

                _target = new SignatureValidationMessageHandler(
                    _httpClient,
                    _validatorStateService.Object,
                    _packageSigningStateService.Object,
                    _logger.Object);
            }

            [Theory]
            [InlineData(ValidationStatus.NotStarted)]
            [InlineData(ValidationStatus.Failed)]
            [InlineData(ValidationStatus.Succeeded)]
            public async Task DoesNotReprocessCompletedValidations(ValidationStatus state)
            {
                // Arrange
                _validation.State = state;

                // Act
                var success = await _target.HandleAsync(_signedPackage1);

                // Assert
                Assert.True(success, "The handler should have succeeded processing the message.");
                _validatorStateService.Verify(
                    x => x.GetStatusAsync(It.IsAny<Guid>()),
                    Times.Once);
                _validatorStateService.Verify(
                    x => x.SaveStatusAsync(It.IsAny<ValidatorStatus>()),
                    Times.Never);
                _packageSigningStateService.Verify(
                    x => x.SetPackageSigningState(
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<PackageSigningStatus>()),
                    Times.Never);
            }

            [Fact]
            public async Task RejectsSignedPackages()
            {
                // Arrange & Act
                var success = await _target.HandleAsync(_signedPackage1);

                // Assert
                Assert.True(success, "The handler should have succeeded processing the message.");
                _validatorStateService.Verify(
                    x => x.SaveStatusAsync(It.IsAny<ValidatorStatus>()),
                    Times.Once);
                Assert.Equal(ValidationStatus.Failed, _validation.State);
                _packageSigningStateService.Verify(
                    x => x.SetPackageSigningState(
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<PackageSigningStatus>()),
                    Times.Never);
            }

            [Fact]
            public async Task SucceedsWithUnsignedPackages()
            {
                // Arrange & Act
                var success = await _target.HandleAsync(_unsignedPackage);

                // Assert
                Assert.True(success, "The handler should have succeeded processing the message.");
                _validatorStateService.Verify(
                    x => x.SaveStatusAsync(It.IsAny<ValidatorStatus>()),
                    Times.Once);
                Assert.Equal(ValidationStatus.Succeeded, _validation.State);
                _packageSigningStateService.Verify(
                    x => x.SetPackageSigningState(
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<PackageSigningStatus>()),
                    Times.Once);
                _packageSigningStateService.Verify(
                    x => x.SetPackageSigningState(
                        _validation.PackageKey,
                        _unsignedPackage.PackageId,
                        _unsignedPackage.PackageVersion,
                        PackageSigningStatus.Unsigned),
                    Times.Once);
            }
        }

        private class EmbeddedResourceTestHandler : HttpMessageHandler
        {
            private readonly IReadOnlyDictionary<Uri, string> _urlToResourceName;

            public EmbeddedResourceTestHandler(IReadOnlyDictionary<Uri, string> urlToResourceName)
            {
                _urlToResourceName = urlToResourceName;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(Send(request));
            }

            private HttpResponseMessage Send(HttpRequestMessage request)
            {
                if (request.Method != HttpMethod.Get
                    || !_urlToResourceName.TryGetValue(request.RequestUri, out var resourceName))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                var resourceStream = GetType().Assembly.GetManifestResourceStream(resourceName);
                if (resourceStream == null)
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    RequestMessage = request,
                    Content = new StreamContent(resourceStream),
                };
            }
        }
    }
}
