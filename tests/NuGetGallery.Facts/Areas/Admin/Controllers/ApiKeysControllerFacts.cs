// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web;
using System.Net;
using System.Web.Mvc;
using System.Collections.Generic;
using Moq;
using Xunit;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using NuGetGallery.Areas.Admin.ViewModels;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class ApiKeysControllerFacts
    {
        private readonly Mock<IAuthenticationService> _authenticationService;
        private readonly Mock<HttpContextBase> _httpContextBase;
        private readonly Mock<ITelemetryService> _telemetryService;

        public ApiKeysControllerFacts()
        {
            _authenticationService = new Mock<IAuthenticationService>();
            _httpContextBase = new Mock<HttpContextBase>();
            _telemetryService = new Mock<ITelemetryService>();
        }

        [Fact]
        public void GivenNotExistedApiKey_ItReturnsResultWithNullApiKeyViewModel()
        {
            // Arrange
            var verifyQuery = "{\"ApiKey\":\"apiKey1\",\"RevokedBy\":\"GitHub\",\"LeakedUrl\":\"https://leakedUrl1\"}";
            var _authenticationService = new Mock<IAuthenticationService>();
            _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>()))
                .Returns(() => null);

            var apiKeysController = new ApiKeysController(_authenticationService.Object, _telemetryService.Object);
            TestUtility.SetupHttpContextMockForUrlGeneration(_httpContextBase, apiKeysController);

            // Act
            var result = apiKeysController.Verify(verifyQuery);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.Equal((int)HttpStatusCode.OK, apiKeysController.Response.StatusCode);
            var apiKeyRevokeViewModels = Assert.IsType<List<ApiKeyRevokeViewModel>>(jsonResult.Data);

            Assert.Equal(1, apiKeyRevokeViewModels.Count);
            var apiKeyRevokeViewModel = Assert.IsType<ApiKeyRevokeViewModel>(apiKeyRevokeViewModels[0]);
            Assert.Null(apiKeyRevokeViewModel.ApiKeyViewModel);
            Assert.Null(apiKeyRevokeViewModel.RevokedBy);
            Assert.Null(apiKeyRevokeViewModel.LeakedUrl);
            Assert.Equal("apiKey1", apiKeyRevokeViewModel.ApiKey);
            Assert.False(apiKeyRevokeViewModel.IsRevocable);

            _authenticationService.Verify(x => x.GetApiKeyCredential(It.IsAny<string>()), Times.Once);
            _authenticationService.Verify(x => x.DescribeCredential(It.IsAny<Credential>()), Times.Never);
        }

        [Theory]
        [InlineData(CredentialTypes.ApiKey.V1, false, true)]
        [InlineData(CredentialTypes.ApiKey.V1, true, false)]
        [InlineData(CredentialTypes.ApiKey.V2, false, true)]
        [InlineData(CredentialTypes.ApiKey.V2, true, false)]
        [InlineData(CredentialTypes.ApiKey.V3, false, true)]
        [InlineData(CredentialTypes.ApiKey.V3, true, false)]
        [InlineData(CredentialTypes.ApiKey.V4, false, true)]
        [InlineData(CredentialTypes.ApiKey.V4, true, false)]
        public void GivenExistedApiKey_ItReturnsResultWithApiKeyViewModel(string apiKeyType, bool hasExpired, bool isRevocable)
        {
            // Arrange
            var verifyQuery = "{\"ApiKey\":\"apiKey1\",\"RevokedBy\":\"GitHub\",\"LeakedUrl\":\"https://leakedUrl1\"}";
            var _authenticationService = new Mock<IAuthenticationService>();

            _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>()))
                .Returns(() => new Credential());
            _authenticationService.Setup(x => x.DescribeCredential(It.IsAny<Credential>()))
                .Returns(() => GetCredentialViewModel(apiKeyType, hasExpired));

            var apiKeysController = new ApiKeysController(_authenticationService.Object, _telemetryService.Object);
            TestUtility.SetupHttpContextMockForUrlGeneration(_httpContextBase, apiKeysController);

            // Act
            var result = apiKeysController.Verify(verifyQuery);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.Equal((int)HttpStatusCode.OK, apiKeysController.Response.StatusCode);
            var apiKeyRevokeViewModels = Assert.IsType<List<ApiKeyRevokeViewModel>>(jsonResult.Data);

            Assert.Equal(1, apiKeyRevokeViewModels.Count);
            var apiKeyRevokeViewModel = Assert.IsType<ApiKeyRevokeViewModel>(apiKeyRevokeViewModels[0]);

            Assert.Equal(apiKeyType, apiKeyRevokeViewModel.ApiKeyViewModel.Type);
            Assert.Equal(hasExpired, apiKeyRevokeViewModel.ApiKeyViewModel.HasExpired);
            Assert.Equal("apiKey1", apiKeyRevokeViewModel.ApiKey);
            Assert.Equal(isRevocable, apiKeyRevokeViewModel.IsRevocable);

            _authenticationService.Verify(x => x.GetApiKeyCredential(It.IsAny<string>()), Times.Once);
            _authenticationService.Verify(x => x.DescribeCredential(It.IsAny<Credential>()), Times.Once);
        }

        [Theory]
        [MemberData(nameof(VerifyQueriesAndExpectedResults))]
        public void GivenMultipleApiKeys_ItReturnsNotRepeatedResults(string verifyQuery, List<string> expectedApiKeys, List<string> expectedLeakedUrls)
        {
            // Arrange
            var _authenticationService = new Mock<IAuthenticationService>();

            _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>()))
                .Returns(() => new Credential());
            _authenticationService.Setup(x => x.DescribeCredential(It.IsAny<Credential>()))
                .Returns(() => GetCredentialViewModel(CredentialTypes.ApiKey.V4, false));

            var apiKeysController = new ApiKeysController(_authenticationService.Object, _telemetryService.Object);
            TestUtility.SetupHttpContextMockForUrlGeneration(_httpContextBase, apiKeysController);

            // Act
            var result = apiKeysController.Verify(verifyQuery);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.Equal((int)HttpStatusCode.OK, apiKeysController.Response.StatusCode);
            var apiKeyRevokeViewModels = Assert.IsType<List<ApiKeyRevokeViewModel>>(jsonResult.Data);

            Assert.Equal(expectedApiKeys.Count, apiKeyRevokeViewModels.Count);
            for (var i = 0; i < apiKeyRevokeViewModels.Count; i++)
            {
                Assert.Equal(expectedApiKeys[i], apiKeyRevokeViewModels[i].ApiKey);
                Assert.Equal(expectedLeakedUrls[i], apiKeyRevokeViewModels[i].LeakedUrl);
                Assert.Equal(true, apiKeyRevokeViewModels[i].IsRevocable);
            }

            _authenticationService.Verify(x => x.GetApiKeyCredential(It.IsAny<string>()), Times.Exactly(expectedApiKeys.Count));
            _authenticationService.Verify(x => x.DescribeCredential(It.IsAny<Credential>()), Times.Exactly(expectedApiKeys.Count));
        }

        public static IEnumerable<object[]> VerifyQueriesAndExpectedResults
        {
            get
            {
                yield return new object[] { "{\"ApiKey\":\"apiKey1\",\"RevokedBy\":\"GitHub\",\"LeakedUrl\":\"https://leakedUrl1\"} \n" +
                                            "{\"ApiKey\":\"apiKey2\",\"RevokedBy\":\"GitHub\",\"LeakedUrl\":\"https://leakedUrl2\"} \n" +
                                            "{\"ApiKey\":\"apiKey3\",\"RevokedBy\":\"GitHub\",\"LeakedUrl\":\"https://leakedUrl3\"} \n",
                                            new List<string>{"apiKey1", "apiKey2", "apiKey3" },
                                            new List<string>{ "https://leakedUrl1", "https://leakedUrl2", "https://leakedUrl3"} };
                yield return new object[] { "{\"ApiKey\":\"apiKey1\",\"RevokedBy\":\"GitHub\",\"LeakedUrl\":\"https://leakedUrl1\"} \n" +
                                            "{\"ApiKey\":\"apiKey1\",\"RevokedBy\":\"GitHub\",\"LeakedUrl\":\"https://leakedUrl1\"} \n" +
                                            "{\"ApiKey\":\"apiKey2\",\"RevokedBy\":\"GitHub\",\"LeakedUrl\":\"https://leakedUrl2\"} \n",
                                            new List<string>{"apiKey1", "apiKey2" },
                                            new List<string>{ "https://leakedUrl1", "https://leakedUrl2" } };
                yield return new object[] { "{\"ApiKey\":\"apiKey1\",\"RevokedBy\":\"GitHub\",\"LeakedUrl\":\"https://leakedUrl1\"} \n" +
                                            "{\"ApiKey\":\"APIKEY1\",\"RevokedBy\":\"GitHub\",\"LeakedUrl\":\"https://leakedUrl1\"} \n" +
                                            "{\"ApiKey\":\"apiKey2\",\"RevokedBy\":\"GitHub\",\"LeakedUrl\":\"https://leakedUrl2\"} \n",
                                            new List<string>{"apiKey1", "apiKey2" },
                                            new List<string>{ "https://leakedUrl1", "https://leakedUrl2" } };
            }
        }

        private CredentialViewModel GetCredentialViewModel(string apiKeyType, bool hasExpired)
        {
            var credentialViewModel = new CredentialViewModel();
            credentialViewModel.Type = apiKeyType;
            credentialViewModel.HasExpired = hasExpired;
            credentialViewModel.Scopes = new List<ScopeViewModel>();

            return credentialViewModel;
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        [InlineData("\n")]
        [InlineData("\t")]
        [InlineData("\n\t")]
        [InlineData("\t\n")]
        [InlineData("\n\n")]
        [InlineData("\t\t")]
        public void GivenEmptyVerifyQuery_ItReturnsWarning(string verifyQuery)
        {
            // Arrange
            var apiKeysController = new ApiKeysController(_authenticationService.Object, _telemetryService.Object);
            TestUtility.SetupHttpContextMockForUrlGeneration(_httpContextBase, apiKeysController);

            // Act
            var result = apiKeysController.Verify(verifyQuery);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.Equal((int)HttpStatusCode.BadRequest, apiKeysController.Response.StatusCode);
            Assert.Equal("Invalid empty input!", jsonResult.Data);
        }

        [Theory]
        [InlineData("testQuery", "testQuery")]
        [InlineData("{\"ApiKey\":\"apiKey1\"", "{\"ApiKey\":\"apiKey1\"")]
        [InlineData("{\"ApiKey\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\"", "{\"ApiKey\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\"")]
        [InlineData("{\"ApiKey\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\"} \n" + "{\"ApiKey\":\"apiKey2\",\"LeakedUrl\":\"https://leakedUrl2\"",
                    "{\"ApiKey\":\"apiKey2\",\"LeakedUrl\":\"https://leakedUrl2\"")]
        public void GivenInvalidVerifyQuery_ItReturnsWarning(string verifyQuery, string expectedMessageQuery)
        {
            // Arrange
            var _authenticationService = new Mock<IAuthenticationService>();

            var apiKeysController = new ApiKeysController(_authenticationService.Object, _telemetryService.Object);
            TestUtility.SetupHttpContextMockForUrlGeneration(_httpContextBase, apiKeysController);

            // Act
            var result = apiKeysController.Verify(verifyQuery);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.Equal((int)HttpStatusCode.BadRequest, apiKeysController.Response.StatusCode);
            Assert.Equal($"Invalid input! {expectedMessageQuery} is not using the valid JSON format.", jsonResult.Data);
        }

        [Fact]
        public void GivenVerifyQuery_ItThrowsExceptionFromDependencies()
        {
            // Arrange
            var verifyQuery = "{\"ApiKey\":\"apiKey1\",\"RevokedBy\":\"GitHub\",\"LeakedUrl\":\"https://leakedUrl1\"}";
            var exceptionMessage = "Some exceptions!";
            var _authenticationService = new Mock<IAuthenticationService>();

            _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>()))
                .Throws(new Exception(exceptionMessage));

            var apiKeysController = new ApiKeysController(_authenticationService.Object, _telemetryService.Object);
            TestUtility.SetupHttpContextMockForUrlGeneration(_httpContextBase, apiKeysController);

            // Act and Assert
            var exception = Assert.Throws<Exception>(() => apiKeysController.Verify(verifyQuery));
            Assert.Equal(exceptionMessage, exception.Message);
        }
    }
}