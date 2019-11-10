// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Web;
using System.Linq;
using System.Web.Mvc;
using System.Threading.Tasks;
using System.Collections.Generic;
using Moq;
using Xunit;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Authentication;
using NuGetGallery.Areas.Admin.Models;
using Newtonsoft.Json;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class ApiKeysControllerFacts
    {
        public class TheVerifyMethod
        {
            private readonly Mock<IAuthenticationService> _authenticationService;
            private readonly Mock<HttpContextBase> _httpContextBase;
            private readonly Mock<ITelemetryService> _telemetryService;

            public TheVerifyMethod()
            {
                _authenticationService = new Mock<IAuthenticationService>();
                _httpContextBase = new Mock<HttpContextBase>();
                _telemetryService = new Mock<ITelemetryService>();
            }

            [Fact]
            public void GivenNotExistedApiKey_ItReturnsResultWithNullApiKeyViewModel()
            {
                // Arrange
                var revokedBy = Enum.GetName(typeof(CredentialRevokedByType), CredentialRevokedByType.GitHub);
                var verifyQuery = "{\"ApiKey\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\",\"RevokedBy\":\"" + revokedBy + "\"}";
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
            [InlineData(CredentialTypes.ApiKey.V1, true, null)]
            [InlineData(CredentialTypes.ApiKey.V2, true, null)]
            [InlineData(CredentialTypes.ApiKey.V3, true, null)]
            [InlineData(CredentialTypes.ApiKey.V4, true, null)]
            [InlineData(CredentialTypes.ApiKey.V1, false, "TestRevokedByType")]
            [InlineData(CredentialTypes.ApiKey.V2, false, "TestRevokedByType")]
            [InlineData(CredentialTypes.ApiKey.V3, false, "TestRevokedByType")]
            [InlineData(CredentialTypes.ApiKey.V4, false, "TestRevokedByType")]
            [InlineData(CredentialTypes.ApiKey.V1, true, "TestRevokedByType")]
            [InlineData(CredentialTypes.ApiKey.V2, true, "TestRevokedByType")]
            [InlineData(CredentialTypes.ApiKey.V3, true, "TestRevokedByType")]
            [InlineData(CredentialTypes.ApiKey.V4, true, "TestRevokedByType")]
            [InlineData(CredentialTypes.External.MicrosoftAccount, false, null)]
            [InlineData(CredentialTypes.External.AzureActiveDirectoryAccount, false, null)]
            [InlineData(CredentialTypes.Password.Sha1, false, null)]
            [InlineData(CredentialTypes.Password.Pbkdf2, false, null)]
            [InlineData(CredentialTypes.Password.V3, false, null)]
            public void GivenNotRevocableApiKey_ItReturnsResultWithApiKeyViewModel(string apiKeyType, bool hasExpired, string revokedBy)
            {
                // Arrange
                var verifyQuery = "{\"ApiKey\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\",\"RevokedBy\":\"AnyRevokedByType\"}";
                _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>()))
                    .Returns(() => new Credential());
                _authenticationService.Setup(x => x.DescribeCredential(It.IsAny<Credential>()))
                    .Returns(() => GetCredentialViewModel(apiKeyType, hasExpired, revokedBy));

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
                Assert.Equal(revokedBy, apiKeyRevokeViewModel.RevokedBy);
                Assert.Null(apiKeyRevokeViewModel.LeakedUrl);
                Assert.False(apiKeyRevokeViewModel.IsRevocable);

                _authenticationService.Verify(x => x.GetApiKeyCredential(It.IsAny<string>()), Times.Once);
                _authenticationService.Verify(x => x.DescribeCredential(It.IsAny<Credential>()), Times.Once);
            }

            [Theory]
            [InlineData(CredentialTypes.ApiKey.V1, CredentialRevokedByType.GitHub, "https://leakedUrl1")]
            [InlineData(CredentialTypes.ApiKey.V2, CredentialRevokedByType.GitHub, "https://leakedUrl1")]
            [InlineData(CredentialTypes.ApiKey.V3, CredentialRevokedByType.GitHub, "https://leakedUrl1")]
            [InlineData(CredentialTypes.ApiKey.V4, CredentialRevokedByType.GitHub, "https://leakedUrl1")]
            [InlineData(CredentialTypes.ApiKey.V1, CredentialRevokedByType.GitHub, "https://leakedUrl2")]
            [InlineData(CredentialTypes.ApiKey.V2, CredentialRevokedByType.GitHub, "https://leakedUrl2")]
            [InlineData(CredentialTypes.ApiKey.V3, CredentialRevokedByType.GitHub, "https://leakedUrl2")]
            [InlineData(CredentialTypes.ApiKey.V4, CredentialRevokedByType.GitHub, "https://leakedUrl2")]
            public void GivenRevocableApiKey_ItReturnsResultWithApiKeyViewModel(string apiKeyType, CredentialRevokedByType revokedByType, string leakedUrl)
            {
                // Arrange
                var revokedBy = Enum.GetName(typeof(CredentialRevokedByType), revokedByType);
                var verifyQuery = "{\"ApiKey\":\"apiKey1\",\"LeakedUrl\":\"" + leakedUrl + "\",\"RevokedBy\":\"" + revokedBy + "\"}";
                _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>()))
                    .Returns(() => new Credential());
                _authenticationService.Setup(x => x.DescribeCredential(It.IsAny<Credential>()))
                    .Returns(() => GetCredentialViewModel(apiKeyType, false, null));

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
                Assert.False(apiKeyRevokeViewModel.ApiKeyViewModel.HasExpired);
                Assert.Equal("apiKey1", apiKeyRevokeViewModel.ApiKey);
                Assert.Equal(revokedBy, apiKeyRevokeViewModel.RevokedBy);
                Assert.Equal(leakedUrl, apiKeyRevokeViewModel.LeakedUrl);
                Assert.True(apiKeyRevokeViewModel.IsRevocable);

                _authenticationService.Verify(x => x.GetApiKeyCredential(It.IsAny<string>()), Times.Once);
                _authenticationService.Verify(x => x.DescribeCredential(It.IsAny<Credential>()), Times.Once);
            }

            [Theory]
            [MemberData(nameof(VerifyQueriesAndExpectedResults))]
            public void GivenMultipleApiKeys_ItReturnsNotRepeatedResults(string verifyQuery, List<string> expectedApiKeys, List<string> expectedLeakedUrls)
            {
                // Arrange
                _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>()))
                    .Returns(() => new Credential());
                _authenticationService.Setup(x => x.DescribeCredential(It.IsAny<Credential>()))
                    .Returns(() => GetCredentialViewModel(CredentialTypes.ApiKey.V4, false, null));

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

            private CredentialViewModel GetCredentialViewModel(string apiKeyType, bool hasExpired, string revokedBy)
            {
                var credentialViewModel = new CredentialViewModel();
                credentialViewModel.Type = apiKeyType;
                credentialViewModel.HasExpired = hasExpired;
                credentialViewModel.RevokedBy = revokedBy;
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
            [InlineData("{\"ApiKey\":\"apiKey1\"}",
                        "{\"ApiKey\":\"apiKey1\"}")]
            [InlineData("{\"LeakedUrl\":\"https://leakedUrl1\"}",
                        "{\"LeakedUrl\":\"https://leakedUrl1\"}")]
            [InlineData("{\"RevokedBy\":\"AnyRevokedByType\"}",
                        "{\"RevokedBy\":\"AnyRevokedByType\"}")]
            [InlineData("{\"ApiKey\":\"apiKey1\",\"RevokedBy\":\"AnyRevokedByType\"}",
                        "{\"ApiKey\":\"apiKey1\",\"RevokedBy\":\"AnyRevokedByType\"}")]
            [InlineData("{\"ApiKey\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\"}",
                        "{\"ApiKey\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\"}")]
            [InlineData("{\"RevokedBy\":\"AnyRevokedByType\",\"LeakedUrl\":\"https://leakedUrl1\"}",
                        "{\"RevokedBy\":\"AnyRevokedByType\",\"LeakedUrl\":\"https://leakedUrl1\"}")]
            [InlineData("{\"Api\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\",\"RevokedBy\":\"AnyRevokedByType\"}",
                        "{\"Api\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\",\"RevokedBy\":\"AnyRevokedByType\"}")]
            [InlineData("{\"ApiKey\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\",\"RevokedBy\":\"AnyRevokedByType\"",
                        "{\"ApiKey\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\",\"RevokedBy\":\"AnyRevokedByType\"")]
            [InlineData("{\"ApiKey\":\"apiKey1\",\"RevokedBy\":\"AnyRevokedByType\",\"LeakedUrl\":\"https://leakedUrl1\"} \n" +
                        "{\"ApiKey\":\"apiKey2\",\"RevokedBy\":\"AnyRevokedByType\"",
                        "{\"ApiKey\":\"apiKey2\",\"RevokedBy\":\"AnyRevokedByType\"")]
            [InlineData("{\"ApiKey\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\",\"RevokedBy\":\"AnyRevokedByType\"} \n" +
                        "{\"ApiKey\":\"apiKey2\",\"RevokedBy\":\"AnyRevokedByType\" \n" +
                        "{\"ApiKey\":\"apiKey3\",\"RevokedBy\":\"AnyRevokedByType\"",
                        "{\"ApiKey\":\"apiKey2\",\"RevokedBy\":\"AnyRevokedByType\"")]
            public void GivenInvalidVerifyQuery_ItReturnsWarning(string verifyQuery, string expectedMessageQuery)
            {
                // Arrange
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
            public void GivenVerifyQueryWithNotSupportedRevokedByType_ItReturnsWarning()
            {
                // Arrange
                var revokedBy = "AnyOtherRevokedByType";
                var verifyQuery = "{\"ApiKey\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\",\"RevokedBy\":\"" + revokedBy + "\"}";
                _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>()))
                    .Returns(() => new Credential());
                _authenticationService.Setup(x => x.DescribeCredential(It.IsAny<Credential>()))
                    .Returns(() => GetCredentialViewModel(CredentialTypes.ApiKey.V4, false, null));

                var apiKeysController = new ApiKeysController(_authenticationService.Object, _telemetryService.Object);
                TestUtility.SetupHttpContextMockForUrlGeneration(_httpContextBase, apiKeysController);

                // Act
                var result = apiKeysController.Verify(verifyQuery);

                // Assert
                var jsonResult = Assert.IsType<JsonResult>(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, apiKeysController.Response.StatusCode);
                Assert.Equal($"Invalid input! {verifyQuery} is not using the supported revokedBy types: " +
                            $"{string.Join(",", Enum.GetNames(typeof(CredentialRevokedByType)))}.", jsonResult.Data);
            }

            [Fact]
            public void GivenVerifyQuery_ItThrowsExceptionFromDependencies()
            {
                // Arrange
                var verifyQuery = "{\"ApiKey\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\",\"RevokedBy\":\"AnyRevokedByType\"}";
                var exceptionMessage = "Some exceptions!";

                _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>()))
                    .Throws(new Exception(exceptionMessage));

                var apiKeysController = new ApiKeysController(_authenticationService.Object, _telemetryService.Object);
                TestUtility.SetupHttpContextMockForUrlGeneration(_httpContextBase, apiKeysController);

                // Act and Assert
                var exception = Assert.Throws<Exception>(() => apiKeysController.Verify(verifyQuery));
                Assert.Equal(exceptionMessage, exception.Message);
            }
        }

        public class TheRevokeMethod
        {
            private readonly Mock<IAuthenticationService> _authenticationService;
            private readonly Mock<HttpContextBase> _httpContextBase;
            private readonly Mock<ITelemetryService> _telemetryService;

            public TheRevokeMethod()
            {
                _authenticationService = new Mock<IAuthenticationService>();
                _httpContextBase = new Mock<HttpContextBase>();
                _telemetryService = new Mock<ITelemetryService>();
            }

            [Fact]
            public void GivenValidRequest_ItRevokesAPIKeys()
            {
                // Arrange
                _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>())).Returns(new Credential()).Verifiable();
                _authenticationService.Setup(x => x.RevokeCredential(It.IsAny<Credential>(), It.IsAny<CredentialRevokedByType>()))
                    .Returns(Task.FromResult(0)).Verifiable();

                var apiKeysController = new ApiKeysController(_authenticationService.Object, _telemetryService.Object);
                TestUtility.SetupHttpContextMockForUrlGeneration(_httpContextBase, apiKeysController);

                // Act
                apiKeysController.Revoke(GetRevokeApiKeysRequest());

                // Assert
                _authenticationService.VerifyAll();
                Assert.Equal("Successfully revoke the selected API keys.", apiKeysController.TempData["Message"]);
            }

            [Fact]
            public void GivenNullRequest_ItReturnsErrorMessage()
            {
                // Arrange
                var apiKeysController = new ApiKeysController(_authenticationService.Object, _telemetryService.Object);
                TestUtility.SetupHttpContextMockForUrlGeneration(_httpContextBase, apiKeysController);

                // Act
                apiKeysController.Revoke(null);

                // Assert
                Assert.Equal("The API keys revoking request can not be null.", apiKeysController.TempData["ErrorMessage"]);
            }

            [Fact]
            public void ThrowExceptionsFromGetApiKeyCredential_ItReturnsErrorMessage()
            {
                // Arrange
                var apiKeysController = new ApiKeysController(_authenticationService.Object, _telemetryService.Object);
                TestUtility.SetupHttpContextMockForUrlGeneration(_httpContextBase, apiKeysController);

                var exception = new Exception("Some exceptions!");
                _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>())).Throws(exception);
                _telemetryService.Setup(x => x.TraceException(exception));

                // Act
                apiKeysController.Revoke(GetRevokeApiKeysRequest());
                Assert.Equal($"Failed to revoke the API key(s): apiKey1, apiKey2." +
                    $"Please check the telemetry for details.", apiKeysController.TempData["ErrorMessage"]);
                _telemetryService.Verify(x => x.TraceException(exception), Times.Exactly(2));
            }

            [Fact]
            public void ThrowExceptionsFromRevokeCredential_ItReturnsErrorMessage()
            {
                // Arrange
                var apiKeysController = new ApiKeysController(_authenticationService.Object, _telemetryService.Object);
                TestUtility.SetupHttpContextMockForUrlGeneration(_httpContextBase, apiKeysController);

                var exception = new Exception("Some exceptions!");
                _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>())).Returns(new Credential());
                _authenticationService.Setup(x => x.RevokeCredential(It.IsAny<Credential>(), It.IsAny<CredentialRevokedByType>())).Throws(exception);
                _telemetryService.Setup(x => x.TraceException(exception));

                // Act
                apiKeysController.Revoke(GetRevokeApiKeysRequest());
                Assert.Equal($"Failed to revoke the API key(s): apiKey1, apiKey2." +
                    $"Please check the telemetry for details.", apiKeysController.TempData["ErrorMessage"]);
                _telemetryService.Verify(x => x.TraceException(exception), Times.Exactly(2));
            }

            private RevokeApiKeysRequest GetRevokeApiKeysRequest()
            {
                var revokeApiKeysRequest = new RevokeApiKeysRequest();
                var apiKeyRevokeViewModel1 = new ApiKeyRevokeViewModel(null, "apiKey1", "https://leakedUrl1",
                    Enum.GetName(typeof(CredentialRevokedByType), CredentialRevokedByType.GitHub), true);
                var apiKeyRevokeViewModel2 = new ApiKeyRevokeViewModel(null, "apiKey2", "https://leakedUrl2",
                    Enum.GetName(typeof(CredentialRevokedByType), CredentialRevokedByType.GitHub), true);
                revokeApiKeysRequest.SelectedApiKeys = new List<string> {
                    JsonConvert.SerializeObject(apiKeyRevokeViewModel1),
                    JsonConvert.SerializeObject(apiKeyRevokeViewModel2) };

                return revokeApiKeysRequest;
            }
        }
    }
}