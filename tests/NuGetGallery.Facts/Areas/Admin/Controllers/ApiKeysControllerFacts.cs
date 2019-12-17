// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Web.Mvc;
using System.Threading.Tasks;
using System.Collections.Generic;
using Moq;
using Xunit;
using Newtonsoft.Json;
using NuGetGallery.Framework;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using NuGet.Services.Messaging.Email;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Infrastructure.Mail.Messages;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class ApiKeysControllerFacts
    {
        public class TheVerifyMethod : TestContainer
        {
            private readonly Mock<IAuthenticationService> _authenticationService;
            public TheVerifyMethod()
            {
                _authenticationService = GetMock<IAuthenticationService>();
            }

            [Fact]
            public void GivenNotExistedApiKey_ItReturnsResultWithNullApiKeyViewModel()
            {
                // Arrange
                var verifyQuery = "{\"ApiKey\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\",\"RevocationSource\":\"AnyRevocationSource\"}";
                _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>()))
                    .Returns(() => null);

                var apiKeysController = GetController<ApiKeysController>();

                // Act
                var result = apiKeysController.Verify(verifyQuery);

                // Assert
                var jsonResult = Assert.IsType<JsonResult>(result);
                Assert.Equal((int)HttpStatusCode.OK, apiKeysController.Response.StatusCode);
                var apiKeyRevokeViewModels = Assert.IsType<List<ApiKeyRevokeViewModel>>(jsonResult.Data);

                Assert.Equal(1, apiKeyRevokeViewModels.Count);
                var apiKeyRevokeViewModel = Assert.IsType<ApiKeyRevokeViewModel>(apiKeyRevokeViewModels[0]);
                Assert.Null(apiKeyRevokeViewModel.ApiKeyViewModel);
                Assert.Null(apiKeyRevokeViewModel.RevocationSource);
                Assert.Null(apiKeyRevokeViewModel.LeakedUrl);
                Assert.Equal("apiKey1", apiKeyRevokeViewModel.ApiKey);
                Assert.False(apiKeyRevokeViewModel.IsRevocable);

                _authenticationService.Verify(x => x.GetApiKeyCredential(It.IsAny<string>()), Times.Once);
                _authenticationService.Verify(x => x.DescribeCredential(It.IsAny<Credential>()), Times.Never);
                _authenticationService.Verify(x => x.IsActiveApiKeyCredential(It.IsAny<Credential>()), Times.Never);
            }

            [Theory]
            [InlineData(CredentialTypes.ApiKey.V1, null)]
            [InlineData(CredentialTypes.ApiKey.V2, null)]
            [InlineData(CredentialTypes.ApiKey.V3, null)]
            [InlineData(CredentialTypes.ApiKey.V4, null)]
            [InlineData(CredentialTypes.ApiKey.V1, "TestRevocationSource")]
            [InlineData(CredentialTypes.ApiKey.V2, "TestRevocationSource")]
            [InlineData(CredentialTypes.ApiKey.V3, "TestRevocationSource")]
            [InlineData(CredentialTypes.ApiKey.V4, "TestRevocationSource")]
            public void GivenNotRevocableApiKey_ItReturnsResultWithApiKeyViewModel(string apiKeyType, string revocationSource)
            {
                // Arrange
                var verifyQuery = "{\"ApiKey\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\",\"RevocationSource\":\"AnyRevocationSource\"}";
                _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>()))
                    .Returns(() => new Credential());
                _authenticationService.Setup(x => x.DescribeCredential(It.IsAny<Credential>()))
                    .Returns(GetApiKeyCredentialViewModel(apiKeyType, revocationSource));
                _authenticationService.Setup(x => x.IsActiveApiKeyCredential(It.IsAny<Credential>()))
                    .Returns(false);

                var apiKeysController = GetController<ApiKeysController>();

                // Act
                var result = apiKeysController.Verify(verifyQuery);

                // Assert
                var jsonResult = Assert.IsType<JsonResult>(result);
                Assert.Equal((int)HttpStatusCode.OK, apiKeysController.Response.StatusCode);
                var apiKeyRevokeViewModels = Assert.IsType<List<ApiKeyRevokeViewModel>>(jsonResult.Data);

                Assert.Equal(1, apiKeyRevokeViewModels.Count);
                var apiKeyRevokeViewModel = Assert.IsType<ApiKeyRevokeViewModel>(apiKeyRevokeViewModels[0]);

                Assert.Equal(apiKeyType, apiKeyRevokeViewModel.ApiKeyViewModel.Type);
                Assert.Equal("apiKey1", apiKeyRevokeViewModel.ApiKey);
                Assert.Equal(revocationSource, apiKeyRevokeViewModel.RevocationSource);
                Assert.Null(apiKeyRevokeViewModel.LeakedUrl);
                Assert.False(apiKeyRevokeViewModel.IsRevocable);

                _authenticationService.Verify(x => x.GetApiKeyCredential(It.IsAny<string>()), Times.Once);
                _authenticationService.Verify(x => x.DescribeCredential(It.IsAny<Credential>()), Times.Once);
                _authenticationService.Verify(x => x.IsActiveApiKeyCredential(It.IsAny<Credential>()), Times.Once);
            }

            [Theory]
            [InlineData(CredentialTypes.ApiKey.V1, CredentialRevocationSource.GitHub, "https://leakedUrl1")]
            [InlineData(CredentialTypes.ApiKey.V2, CredentialRevocationSource.GitHub, "https://leakedUrl1")]
            [InlineData(CredentialTypes.ApiKey.V3, CredentialRevocationSource.GitHub, "https://leakedUrl1")]
            [InlineData(CredentialTypes.ApiKey.V4, CredentialRevocationSource.GitHub, "https://leakedUrl1")]
            [InlineData(CredentialTypes.ApiKey.V1, CredentialRevocationSource.GitHub, "https://leakedUrl2")]
            [InlineData(CredentialTypes.ApiKey.V2, CredentialRevocationSource.GitHub, "https://leakedUrl2")]
            [InlineData(CredentialTypes.ApiKey.V3, CredentialRevocationSource.GitHub, "https://leakedUrl2")]
            [InlineData(CredentialTypes.ApiKey.V4, CredentialRevocationSource.GitHub, "https://leakedUrl2")]
            public void GivenRevocableApiKey_ItReturnsResultWithApiKeyViewModel(string apiKeyType, CredentialRevocationSource revocationSourceKey, string leakedUrl)
            {
                // Arrange
                var revocationSource = Enum.GetName(typeof(CredentialRevocationSource), revocationSourceKey);
                var verifyQuery = "{\"ApiKey\":\"apiKey1\",\"LeakedUrl\":\"" + leakedUrl + "\",\"RevocationSource\":\"" + revocationSource + "\"}";
                _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>()))
                    .Returns(() => new Credential());
                _authenticationService.Setup(x => x.DescribeCredential(It.IsAny<Credential>()))
                    .Returns(GetApiKeyCredentialViewModel(apiKeyType, null));
                _authenticationService.Setup(x => x.IsActiveApiKeyCredential(It.IsAny<Credential>()))
                    .Returns(true);

                var apiKeysController = GetController<ApiKeysController>();

                // Act
                var result = apiKeysController.Verify(verifyQuery);

                // Assert
                var jsonResult = Assert.IsType<JsonResult>(result);
                Assert.Equal((int)HttpStatusCode.OK, apiKeysController.Response.StatusCode);
                var apiKeyRevokeViewModels = Assert.IsType<List<ApiKeyRevokeViewModel>>(jsonResult.Data);

                Assert.Equal(1, apiKeyRevokeViewModels.Count);
                var apiKeyRevokeViewModel = Assert.IsType<ApiKeyRevokeViewModel>(apiKeyRevokeViewModels[0]);

                Assert.Equal(apiKeyType, apiKeyRevokeViewModel.ApiKeyViewModel.Type);
                Assert.Equal("apiKey1", apiKeyRevokeViewModel.ApiKey);
                Assert.Equal(revocationSource, apiKeyRevokeViewModel.RevocationSource);
                Assert.Equal(leakedUrl, apiKeyRevokeViewModel.LeakedUrl);
                Assert.True(apiKeyRevokeViewModel.IsRevocable);

                _authenticationService.Verify(x => x.GetApiKeyCredential(It.IsAny<string>()), Times.Once);
                _authenticationService.Verify(x => x.DescribeCredential(It.IsAny<Credential>()), Times.Once);
                _authenticationService.Verify(x => x.IsActiveApiKeyCredential(It.IsAny<Credential>()), Times.Once);
            }

            [Theory]
            [MemberData(nameof(VerifyQueriesAndExpectedResults))]
            public void GivenMultipleApiKeys_ItReturnsNotRepeatedResults(string verifyQuery, List<string> expectedApiKeys, List<string> expectedLeakedUrls)
            {
                // Arrange
                _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>()))
                    .Returns(() => new Credential());
                _authenticationService.Setup(x => x.DescribeCredential(It.IsAny<Credential>()))
                    .Returns(GetApiKeyCredentialViewModel(CredentialTypes.ApiKey.V4, null));
                _authenticationService.Setup(x => x.IsActiveApiKeyCredential(It.IsAny<Credential>()))
                    .Returns(true);

                var apiKeysController = GetController<ApiKeysController>();

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
                    Assert.Equal("GitHub", apiKeyRevokeViewModels[i].RevocationSource);
                    Assert.True(apiKeyRevokeViewModels[i].IsRevocable);
                }

                _authenticationService.Verify(x => x.GetApiKeyCredential(It.IsAny<string>()), Times.Exactly(expectedApiKeys.Count));
                _authenticationService.Verify(x => x.DescribeCredential(It.IsAny<Credential>()), Times.Exactly(expectedApiKeys.Count));
                _authenticationService.Verify(x => x.IsActiveApiKeyCredential(It.IsAny<Credential>()), Times.Exactly(expectedApiKeys.Count));
            }

            public static IEnumerable<object[]> VerifyQueriesAndExpectedResults
            {
                get
                {
                    yield return new object[] { "{\"ApiKey\":\"apiKey1\",\"RevocationSource\":\"GitHub\",\"LeakedUrl\":\"https://leakedUrl1\"} \n" +
                                            "{\"ApiKey\":\"apiKey2\",\"RevocationSource\":\"GitHub\",\"LeakedUrl\":\"https://leakedUrl2\"} \n" +
                                            "{\"ApiKey\":\"apiKey3\",\"RevocationSource\":\"GitHub\",\"LeakedUrl\":\"https://leakedUrl3\"} \n",
                                            new List<string>{"apiKey1", "apiKey2", "apiKey3" },
                                            new List<string>{ "https://leakedUrl1", "https://leakedUrl2", "https://leakedUrl3"} };
                    yield return new object[] { "{\"ApiKey\":\"apiKey1\",\"RevocationSource\":\"GitHub\",\"LeakedUrl\":\"https://leakedUrl1\"} \n" +
                                            "{\"ApiKey\":\"apiKey1\",\"RevocationSource\":\"GitHub\",\"LeakedUrl\":\"https://leakedUrl1\"} \n" +
                                            "{\"ApiKey\":\"apiKey2\",\"RevocationSource\":\"GitHub\",\"LeakedUrl\":\"https://leakedUrl2\"} \n",
                                            new List<string>{"apiKey1", "apiKey2" },
                                            new List<string>{ "https://leakedUrl1", "https://leakedUrl2" } };
                    yield return new object[] { "{\"ApiKey\":\"apiKey1\",\"RevocationSource\":\"GitHub\",\"LeakedUrl\":\"https://leakedUrl1\"} \n" +
                                            "{\"ApiKey\":\"APIKEY1\",\"RevocationSource\":\"GitHub\",\"LeakedUrl\":\"https://leakedUrl1\"} \n" +
                                            "{\"ApiKey\":\"apiKey2\",\"RevocationSource\":\"GitHub\",\"LeakedUrl\":\"https://leakedUrl2\"} \n",
                                            new List<string>{"apiKey1", "apiKey2" },
                                            new List<string>{ "https://leakedUrl1", "https://leakedUrl2" } };
                }
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
                var apiKeysController = GetController<ApiKeysController>();

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
            [InlineData("{\"RevocationSource\":\"AnyRevocationSource\"}",
                        "{\"RevocationSource\":\"AnyRevocationSource\"}")]
            [InlineData("{\"ApiKey\":\"apiKey1\",\"RevocationSource\":\"AnyRevocationSource\"}",
                        "{\"ApiKey\":\"apiKey1\",\"RevocationSource\":\"AnyRevocationSource\"}")]
            [InlineData("{\"ApiKey\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\"}",
                        "{\"ApiKey\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\"}")]
            [InlineData("{\"RevocationSource\":\"AnyRevocationSource\",\"LeakedUrl\":\"https://leakedUrl1\"}",
                        "{\"RevocationSource\":\"AnyRevocationSource\",\"LeakedUrl\":\"https://leakedUrl1\"}")]
            [InlineData("{\"Api\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\",\"RevocationSource\":\"AnyRevocationSource\"}",
                        "{\"Api\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\",\"RevocationSource\":\"AnyRevocationSource\"}")]
            [InlineData("{\"ApiKey\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\",\"RevocationSource\":\"AnyRevocationSource\"",
                        "{\"ApiKey\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\",\"RevocationSource\":\"AnyRevocationSource\"")]
            [InlineData("{\"ApiKey\":\"apiKey1\",\"RevocationSource\":\"AnyRevocationSource\",\"LeakedUrl\":\"https://leakedUrl1\"} \n" +
                        "{\"ApiKey\":\"apiKey2\",\"RevocationSource\":\"AnyRevocationSource\"",
                        "{\"ApiKey\":\"apiKey2\",\"RevocationSource\":\"AnyRevocationSource\"")]
            [InlineData("{\"ApiKey\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\",\"RevocationSource\":\"AnyRevocationSource\"} \n" +
                        "{\"ApiKey\":\"apiKey2\",\"RevocationSource\":\"AnyRevocationSource\" \n" +
                        "{\"ApiKey\":\"apiKey3\",\"RevocationSource\":\"AnyRevocationSource\"",
                        "{\"ApiKey\":\"apiKey2\",\"RevocationSource\":\"AnyRevocationSource\"")]
            public void GivenInvalidVerifyQuery_ItReturnsWarning(string verifyQuery, string expectedMessageQuery)
            {
                // Arrange
                var apiKeysController = GetController<ApiKeysController>();

                // Act
                var result = apiKeysController.Verify(verifyQuery);

                // Assert
                var jsonResult = Assert.IsType<JsonResult>(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, apiKeysController.Response.StatusCode);
                Assert.Equal($"Invalid input! {expectedMessageQuery} is not using the valid JSON format.", jsonResult.Data);
            }

            [Fact]
            public void GivenVerifyQueryWithNotSupportedRevocationSource_ItReturnsWarning()
            {
                // Arrange
                var verifyQuery = "{\"ApiKey\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\",\"RevocationSource\":\"AnyOtherRevocationSource\"}";
                _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>()))
                    .Returns(() => new Credential());
                _authenticationService.Setup(x => x.DescribeCredential(It.IsAny<Credential>()))
                    .Returns(GetApiKeyCredentialViewModel(CredentialTypes.ApiKey.V4, null));
                _authenticationService.Setup(x => x.IsActiveApiKeyCredential(It.IsAny<Credential>()))
                    .Returns(true);

                var apiKeysController = GetController<ApiKeysController>();

                // Act
                var result = apiKeysController.Verify(verifyQuery);

                // Assert
                var jsonResult = Assert.IsType<JsonResult>(result);
                Assert.Equal((int)HttpStatusCode.BadRequest, apiKeysController.Response.StatusCode);
                Assert.Equal($"Invalid input! {verifyQuery} is not using the supported Revocation Source: " +
                            $"{string.Join(",", Enum.GetNames(typeof(CredentialRevocationSource)))}.", jsonResult.Data);

                _authenticationService.Verify(x => x.GetApiKeyCredential(It.IsAny<string>()), Times.Once);
                _authenticationService.Verify(x => x.DescribeCredential(It.IsAny<Credential>()), Times.Once);
                _authenticationService.Verify(x => x.IsActiveApiKeyCredential(It.IsAny<Credential>()), Times.Once);
            }

            [Fact]
            public void GivenVerifyQuery_ItThrowsExceptionFromDependencies()
            {
                // Arrange
                var verifyQuery = "{\"ApiKey\":\"apiKey1\",\"LeakedUrl\":\"https://leakedUrl1\",\"RevocationSource\":\"AnyRevocationSource\"}";
                var exceptionMessage = "Some exceptions!";

                _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>()))
                    .Throws(new Exception(exceptionMessage));

                var apiKeysController = GetController<ApiKeysController>();

                // Act and Assert
                var exception = Assert.Throws<Exception>(() => apiKeysController.Verify(verifyQuery));
                Assert.Equal(exceptionMessage, exception.Message);
            }

            private CredentialViewModel GetApiKeyCredentialViewModel(string apiKeyType, string revocationSource)
            {
                var credentialViewModel = new CredentialViewModel();
                credentialViewModel.Type = apiKeyType;
                credentialViewModel.RevocationSource = revocationSource;
                credentialViewModel.Scopes = new List<ScopeViewModel>();

                return credentialViewModel;
            }
        }

        public class TheRevokeMethod : TestContainer
        {
            private readonly Mock<IAuthenticationService> _authenticationService;

            public TheRevokeMethod()
            {
                _authenticationService = GetMock<IAuthenticationService>();
            }

            [Fact]
            public async Task GivenValidRequest_ItRevokesApiKey()
            {
                // Arrange
                _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>()))
                    .Returns(new Credential());
                _authenticationService.Setup(x => x.RevokeApiKeyCredential(It.IsAny<Credential>(), It.IsAny<CredentialRevocationSource>(), It.IsAny<bool>()))
                    .Returns(Task.FromResult(0));

                var apiKeysController = GetController<ApiKeysController>();

                // Act
                await apiKeysController.Revoke(GetRevokeApiKeyRequest());

                // Assert
                Assert.Equal("Successfully revoke the selected API keys.", apiKeysController.TempData["Message"]);
                _authenticationService.Verify(x => x.GetApiKeyCredential(It.IsAny<string>()), Times.Once);
                _authenticationService.Verify(x => x.RevokeApiKeyCredential(It.IsAny<Credential>(), It.IsAny<CredentialRevocationSource>(), It.IsAny<bool>()), Times.Once);
                GetMock<ITelemetryService>().Verify(x => x.TraceException(It.IsAny<Exception>()), Times.Never);
                GetMock<IMessageService>().Verify(x => x.SendMessageAsync(It.IsAny<CredentialRevokedMessage>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Once);
                GetFakeContext().VerifyCommitChanges();
            }

            [Fact]
            public async Task GivenNullRequest_ItReturnsErrorMessage()
            {
                // Arrange
                var apiKeysController = GetController<ApiKeysController>();

                // Act
                await apiKeysController.Revoke(null);

                // Assert
                Assert.Equal("The API keys revoking request can not be null.", apiKeysController.TempData["ErrorMessage"]);
            }

            [Fact]
            public async Task GivenRequestWithNullSelectedApiKeys_ItReturnsErrorMessage()
            {
                // Arrange
                var apiKeysController = GetController<ApiKeysController>();

                // Act
                await apiKeysController.Revoke(new RevokeApiKeysRequest());

                // Assert
                Assert.Equal("The API keys revoking request contains null or empty selected API keys.", apiKeysController.TempData["ErrorMessage"]);
            }

            [Fact]
            public async Task GivenRequestWithEmptySelectedApiKeys_ItReturnsErrorMessage()
            {
                // Arrange
                var apiKeysController = GetController<ApiKeysController>();

                // Act
                var revokeApiKeysRequest = new RevokeApiKeysRequest();
                revokeApiKeysRequest.SelectedApiKeys = new List<string>();
                await apiKeysController.Revoke(revokeApiKeysRequest);

                // Assert
                Assert.Equal("The API keys revoking request contains null or empty selected API keys.", apiKeysController.TempData["ErrorMessage"]);
            }

            [Fact]
            public async Task ThrowExceptionsFromGetApiKeyCredential_ItReturnsErrorMessage()
            {
                // Arrange
                var exception = new Exception("Some exceptions!");
                _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>())).Throws(exception);

                var apiKeysController = GetController<ApiKeysController>();

                // Act
                await apiKeysController.Revoke(GetRevokeApiKeyRequest());

                // Assert
                Assert.Equal("Failed to revoke the API keys, and please check the telemetry for details.", apiKeysController.TempData["ErrorMessage"]);
                _authenticationService.Verify(x => x.GetApiKeyCredential(It.IsAny<string>()), Times.Once);
                _authenticationService.Verify(x => x.RevokeApiKeyCredential(It.IsAny<Credential>(), It.IsAny<CredentialRevocationSource>(), It.IsAny<bool>()), Times.Never);
                GetMock<ITelemetryService>().Verify(x => x.TraceException(exception), Times.Once);
                GetMock<IMessageService>().Verify(x => x.SendMessageAsync(It.IsAny<CredentialRevokedMessage>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
            }

            [Fact]
            public async Task ThrowExceptionsFromRevokeApiKeyCredential_ItReturnsErrorMessage()
            {
                _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>())).Returns(new Credential());
                var exception = new Exception("Some exceptions!");
                _authenticationService.Setup(x => x.RevokeApiKeyCredential(It.IsAny<Credential>(), It.IsAny<CredentialRevocationSource>(), It.IsAny<bool>()))
                    .ThrowsAsync(exception);

                // Arrange
                var apiKeysController = GetController<ApiKeysController>();

                // Act
                await apiKeysController.Revoke(GetRevokeApiKeyRequest());

                // Assert
                Assert.Equal("Failed to revoke the API keys, and please check the telemetry for details.", apiKeysController.TempData["ErrorMessage"]);
                _authenticationService.Verify(x => x.GetApiKeyCredential(It.IsAny<string>()), Times.Once);
                _authenticationService.Verify(x => x.RevokeApiKeyCredential(It.IsAny<Credential>(), It.IsAny<CredentialRevocationSource>(), It.IsAny<bool>()), Times.Once);
                GetMock<ITelemetryService>().Verify(x => x.TraceException(exception), Times.Once);
                GetMock<IMessageService>().Verify(x => x.SendMessageAsync(It.IsAny<CredentialRevokedMessage>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Once);
            }

            [Fact]
            public async Task ThrowExceptionsFromEntitiesContextSaveChanges_ItReturnsErrorMessage()
            {
                // Arrange
                _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>()))
                    .Returns(new Credential());
                _authenticationService.Setup(x => x.RevokeApiKeyCredential(It.IsAny<Credential>(), It.IsAny<CredentialRevocationSource>(), It.IsAny<bool>()))
                    .Returns(Task.FromResult(0));
                var entitiesContext = GetMock<IEntitiesContext>();
                var exception = new Exception("Some exceptions!");
                entitiesContext.Setup(x => x.SaveChangesAsync()).ThrowsAsync(exception);

                var apiKeysController = GetController<ApiKeysController>();

                // Act
                await apiKeysController.Revoke(GetRevokeApiKeyRequest());

                // Assert
                Assert.Equal("Failed to revoke the API keys, and please check the telemetry for details.", apiKeysController.TempData["ErrorMessage"]);
                _authenticationService.Verify(x => x.GetApiKeyCredential(It.IsAny<string>()), Times.Once);
                _authenticationService.Verify(x => x.RevokeApiKeyCredential(It.IsAny<Credential>(), It.IsAny<CredentialRevocationSource>(), It.IsAny<bool>()), Times.Once);
                entitiesContext.Verify(x => x.SaveChangesAsync(), Times.Once);
                GetMock<ITelemetryService>().Verify(x => x.TraceException(exception), Times.Once);
                GetMock<IMessageService>().Verify(x => x.SendMessageAsync(It.IsAny<CredentialRevokedMessage>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Once);
            }

            private RevokeApiKeysRequest GetRevokeApiKeyRequest()
            {
                var revokeApiKeysRequest = new RevokeApiKeysRequest();
                var apiKeyRevokeViewModel = new ApiKeyRevokeViewModel(null, "apiKey1", "https://leakedUrl1",
                    Enum.GetName(typeof(CredentialRevocationSource), CredentialRevocationSource.GitHub), true);
                revokeApiKeysRequest.SelectedApiKeys = new List<string> { JsonConvert.SerializeObject(apiKeyRevokeViewModel) };

                return revokeApiKeysRequest;
            }

            [Fact]
            public async Task GivenValidRequestWithMultipleApiKeys_ItRevokesMultipleApiKeys()
            {
                // Arrange
                _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>()))
                    .Returns(new Credential());
                _authenticationService.Setup(x => x.RevokeApiKeyCredential(It.IsAny<Credential>(), It.IsAny<CredentialRevocationSource>(), It.IsAny<bool>()))
                    .Returns(Task.FromResult(0));

                var apiKeysController = GetController<ApiKeysController>();

                // Act
                await apiKeysController.Revoke(GetRevokeMultipleApiKeysRequest());

                // Assert
                Assert.Equal("Successfully revoke the selected API keys.", apiKeysController.TempData["Message"]);
                _authenticationService.Verify(x => x.GetApiKeyCredential(It.IsAny<string>()), Times.Exactly(2));
                _authenticationService.Verify(x => x.RevokeApiKeyCredential(It.IsAny<Credential>(), It.IsAny<CredentialRevocationSource>(), It.IsAny<bool>()), Times.Exactly(2));
                GetMock<ITelemetryService>().Verify(x => x.TraceException(It.IsAny<Exception>()), Times.Never);
                GetMock<IMessageService>().Verify(x => x.SendMessageAsync(It.IsAny<CredentialRevokedMessage>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Exactly(2));
                GetFakeContext().VerifyCommitChanges();
            }

            [Fact]
            public async Task GivenValidRequestWithMultipleApiKeys_ThrowExceptionsFromRevokeCredential_ItReturnsErrorMessageWithMultipleApiKeys()
            {
                // Arrange
                var exception = new Exception("Some exceptions!");
                _authenticationService.Setup(x => x.GetApiKeyCredential(It.IsAny<string>()))
                    .Returns(new Credential());
                _authenticationService.Setup(x => x.RevokeApiKeyCredential(It.IsAny<Credential>(), It.IsAny<CredentialRevocationSource>(), It.IsAny<bool>()))
                    .ThrowsAsync(exception);

                var apiKeysController = GetController<ApiKeysController>();

                // Act
                await apiKeysController.Revoke(GetRevokeMultipleApiKeysRequest());

                // Assert
                Assert.Equal("Failed to revoke the API keys, and please check the telemetry for details.", apiKeysController.TempData["ErrorMessage"]);
                _authenticationService.Verify(x => x.GetApiKeyCredential(It.IsAny<string>()), Times.Exactly(1));
                _authenticationService.Verify(x => x.RevokeApiKeyCredential(It.IsAny<Credential>(), It.IsAny<CredentialRevocationSource>(), It.IsAny<bool>()), Times.Exactly(1));
                GetMock<ITelemetryService>().Verify(x => x.TraceException(exception), Times.Once);
                GetMock<IMessageService>().Verify(x => x.SendMessageAsync(It.IsAny<CredentialRevokedMessage>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Once);
            }

            private RevokeApiKeysRequest GetRevokeMultipleApiKeysRequest()
            {
                var revokeApiKeysRequest = new RevokeApiKeysRequest();
                var apiKeyRevokeViewModel1 = new ApiKeyRevokeViewModel(null, "apiKey1", "https://leakedUrl1",
                    Enum.GetName(typeof(CredentialRevocationSource), CredentialRevocationSource.GitHub), true);
                var apiKeyRevokeViewModel2 = new ApiKeyRevokeViewModel(null, "apiKey2", "https://leakedUrl2",
                    Enum.GetName(typeof(CredentialRevocationSource), CredentialRevocationSource.GitHub), true);
                revokeApiKeysRequest.SelectedApiKeys = new List<string> {
                    JsonConvert.SerializeObject(apiKeyRevokeViewModel1),
                    JsonConvert.SerializeObject(apiKeyRevokeViewModel2) };

                return revokeApiKeysRequest;
            }
        }
    }
}