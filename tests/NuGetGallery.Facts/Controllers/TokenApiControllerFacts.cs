// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Moq;
using NuGetGallery.Services.Authentication;
using Xunit;
using System.Text.Json;

#nullable enable

namespace NuGetGallery
{
    public class TokenApiControllerFacts
    {
        public class TheCreateTokenMethod : TokenApiControllerFacts
        {
            [Fact]
            public async Task GeneratesApiKey()
            {
                // Act
                var response = await Target.CreateToken(CreateTokenRequest);

                // Assert
                Assert.Equal(HttpStatusCode.OK, (HttpStatusCode)Response.Object.StatusCode);
                var json = GetJsonBody(response, ["api_key", "expires", "token_type"]);
                Assert.Equal("secret", json.GetProperty("api_key").GetString());
                Assert.Equal("2024-10-11T10:33:00.0000000+00:00", json.GetProperty("expires").GetString());
                Assert.Equal("api_key", json.GetProperty("token_type").GetString());

                FederatedCredentialService.Verify(x => x.GenerateApiKeyAsync("jim", "my-jwt", RequestHeaders), Times.Once);
            }

            [Fact]
            public async Task RejectsMissingAuthorizationHeader()
            {
                // Arrange
                RequestHeaders.Remove("Authorization");

                // Act
                var response = await Target.CreateToken(CreateTokenRequest);

                // Assert
                VerifyUnauthorizedError(response, "The Authorization header is missing.");
            }

            [Fact]
            public async Task RejectsMultipleAuthorizationHeaders()
            {
                // Arrange
                RequestHeaders.Add("Authorization", "Bearer b");

                // Act
                var response = await Target.CreateToken(CreateTokenRequest);

                // Assert
                VerifyUnauthorizedError(response, "Only one Authorization header is allowed.");
            }

            [Fact]
            public async Task RejectsWrongAuthorizationScheme()
            {
                // Arrange
                RequestHeaders["Authorization"] = "Basic my-jwt";

                // Act
                var response = await Target.CreateToken(CreateTokenRequest);

                // Assert
                VerifyUnauthorizedError(response, "The Authorization header value must start with 'Bearer '.");
            }

            [Fact]
            public async Task RejectsBearerPrefixAuthorizationScheme()
            {
                // Arrange
                RequestHeaders["Authorization"] = "Bearer2 my-jwt";

                // Act
                var response = await Target.CreateToken(CreateTokenRequest);

                // Assert
                VerifyUnauthorizedError(response, "The Authorization header value must start with 'Bearer '.");
            }

            [Fact]
            public async Task RejectsMissingBearer()
            {
                // Arrange
                RequestHeaders["Authorization"] = "Bearer";

                // Act
                var response = await Target.CreateToken(CreateTokenRequest);

                // Assert
                VerifyUnauthorizedError(response, "The Authorization header value must start with 'Bearer '.");
            }

            [Fact]
            public async Task RejectsEmptyBearer()
            {
                // Arrange
                RequestHeaders["Authorization"] = "Bearer    ";

                // Act
                var response = await Target.CreateToken(CreateTokenRequest);

                // Assert
                VerifyUnauthorizedError(response, "The bearer token is missing from the Authorization header.");
            }

            [Fact]
            public async Task RejectsAuthenticatedUser()
            {
                // Arrange
                Identity.Setup(x => x.IsAuthenticated).Returns(() => true);

                // Act
                var response = await Target.CreateToken(CreateTokenRequest);

                // Assert
                VerifyUnauthorizedError(response, "Only Bearer token authentication is accepted.");
            }

            [Fact]
            public async Task RejectsInvalidContentType()
            {
                // Arrange
                Request.Setup(x => x.ContentType).Returns(() => ";;;;;");

                // Act
                var response = await Target.CreateToken(CreateTokenRequest);

                // Assert
                VerifyError(HttpStatusCode.UnsupportedMediaType, response, "The request must have a Content-Type of 'application/json'.");
            }

            [Fact]
            public async Task RejectsWrongContentType()
            {
                // Arrange
                Request.Setup(x => x.ContentType).Returns(() => "text/plain");

                // Act
                var response = await Target.CreateToken(CreateTokenRequest);

                // Assert
                VerifyError(HttpStatusCode.UnsupportedMediaType, response, "The request must have a Content-Type of 'application/json'.");
            }

            [Fact]
            public async Task RejectsMissingUserAgent()
            {
                // Arrange
                Request.Setup(x => x.UserAgent).Returns(() => null!);

                // Act
                var response = await Target.CreateToken(CreateTokenRequest);

                // Assert
                VerifyError(HttpStatusCode.BadRequest, response, "A User-Agent header is required.");
            }

            [Fact]
            public async Task RejectsMissingUsername()
            {
                // Arrange
                CreateTokenRequest.Username = "  ";

                // Act
                var response = await Target.CreateToken(CreateTokenRequest);

                // Assert
                VerifyError(HttpStatusCode.BadRequest, response, "The username property in the request body is required.");
            }

            [Fact]
            public async Task RejectsMissingTokenType()
            {
                // Arrange
                CreateTokenRequest.Token_Type = " ";

                // Act
                var response = await Target.CreateToken(CreateTokenRequest);

                // Assert
                VerifyError(HttpStatusCode.BadRequest, response, "The token_type property in the request body is required and must set to 'api_key'.");
            }

            [Fact]
            public async Task RejectsWrongTokenType()
            {
                // Arrange
                CreateTokenRequest.Token_Type = "macaroon";

                // Act
                var response = await Target.CreateToken(CreateTokenRequest);

                // Assert
                VerifyError(HttpStatusCode.BadRequest, response, "The token_type property in the request body is required and must set to 'api_key'.");
            }

            [Fact]
            public async Task RejectsBadRequestResult()
            {
                // Arrange
                GenerateApiKeyResult = GenerateApiKeyResult.BadRequest("You dun goofed.");

                // Act
                var response = await Target.CreateToken(CreateTokenRequest);

                // Assert
                VerifyError(HttpStatusCode.BadRequest, response, "You dun goofed.");
            }

            [Fact]
            public async Task RejectsUnauthorizedResult()
            {
                // Arrange
                GenerateApiKeyResult = GenerateApiKeyResult.Unauthorized("Not gonna work, buddy.");

                // Act
                var response = await Target.CreateToken(CreateTokenRequest);

                // Assert
                VerifyUnauthorizedError(response, "Not gonna work, buddy.");
            }
        }

        public TokenApiControllerFacts()
        {
            AuthenticationManager = new Mock<IAuthenticationManager>();
            OwinContext = new Mock<IOwinContext>();
            Request = new Mock<HttpRequestBase>();
            Response = new Mock<HttpResponseBase>();
            HttpContext = new Mock<HttpContextBase>();
            User = new Mock<ClaimsPrincipal>();
            Identity = new Mock<IIdentity>();
            FederatedCredentialService = new Mock<IFederatedCredentialService>();

            RequestHeaders = new NameValueCollection();
            ResponseHeaders = new NameValueCollection();
            BearerToken = "my-jwt";
            GenerateApiKeyResult = GenerateApiKeyResult.Created("secret", new DateTimeOffset(2024, 10, 11, 10, 33, 0, TimeSpan.Zero));
            CreateTokenRequest = new CreateTokenRequest { Username = "jim", Token_Type = "api_key" };

            OwinContext.Setup(x => x.Authentication).Returns(() => AuthenticationManager.Object);
            RequestHeaders["Authorization"] = $"Bearer {BearerToken}";
            User.Setup(x => x.Identity).Returns(() => Identity.Object);
            Identity.Setup(x => x.IsAuthenticated).Returns(() => false);
            Request.Setup(x => x.ContentType).Returns(() => "application/json");
            Request.Setup(x => x.UserAgent).Returns(() => "testbot");

            FederatedCredentialService
                .Setup(x => x.GenerateApiKeyAsync(CreateTokenRequest.Username, BearerToken, RequestHeaders))
                .ReturnsAsync(() => GenerateApiKeyResult);

            Target = new TokenApiController(FederatedCredentialService.Object);

            Target.SetOwinContextOverride(OwinContext.Object);
            Response.SetupProperty(x => x.StatusCode);
            Response.SetupProperty(x => x.StatusDescription);
            Response.SetupProperty(x => x.ContentType);
            Request.Setup(x => x.Headers).Returns(() => RequestHeaders);
            Response.Setup(x => x.Headers).Returns(() => ResponseHeaders);
            HttpContext.Setup(x => x.Request).Returns(() => Request.Object);
            HttpContext.Setup(x => x.Response).Returns(() => Response.Object);
            HttpContext.Setup(x => x.User).Returns(() => User.Object);
            Target.ControllerContext = new ControllerContext(
                new RequestContext(HttpContext.Object, new RouteData()),
                Target);
        }

        public Mock<IAuthenticationManager> AuthenticationManager { get; }
        public Mock<IOwinContext> OwinContext { get; }
        public Mock<HttpRequestBase> Request { get; }
        public Mock<HttpResponseBase> Response { get; }
        public Mock<HttpContextBase> HttpContext { get; }
        public Mock<ClaimsPrincipal> User { get; }
        public Mock<IIdentity> Identity { get; }
        public Mock<IFederatedCredentialService> FederatedCredentialService { get; }
        public NameValueCollection RequestHeaders { get; }
        public NameValueCollection ResponseHeaders { get; }
        public string BearerToken { get; }
        public GenerateApiKeyResult GenerateApiKeyResult { get; set; }
        public CreateTokenRequest CreateTokenRequest { get; }
        public TokenApiController Target { get; }

        public JsonElement GetJsonBody(JsonResult response, IReadOnlyList<string> expectedKeys)
        {
            response.ExecuteResult(Target.ControllerContext);
            Assert.Equal("application/json", Response.Object.ContentType);
            var write = Assert.Single(Response.Invocations.Where(x => x.Method.Name == nameof(HttpResponseBase.Write)));
            var data = Assert.IsType<string>(write.Arguments[0]);
            var doc = JsonSerializer.Deserialize<JsonDocument>(data);
            Assert.NotNull(doc);
            Assert.Equal(
                expectedKeys.OrderBy(x => x, StringComparer.Ordinal),
                doc.RootElement.EnumerateObject().Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal));
            return doc.RootElement;
        }

        public void VerifyUnauthorizedError(JsonResult response, string error)
        {
            VerifyError(HttpStatusCode.Unauthorized, response, error);
            AuthenticationManager.Verify(x => x.Challenge(It.Is<string[]>(a => a[0] == "Federated" && a.Length == 1)), Times.Once);
            Assert.Equal("Bearer", ResponseHeaders["WWW-Authenticate"]);
        }

        public void VerifyError(HttpStatusCode status, JsonResult response, string error)
        {
            Assert.Equal(status, (HttpStatusCode)Response.Object.StatusCode);
            var json = GetJsonBody(response, ["error"]);
            Assert.Equal(error, json.GetProperty("error").GetString());
            Assert.Equal(error, Response.Object.StatusDescription);
        }
    }
}
