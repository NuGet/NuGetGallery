// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Authentication;
using NuGetGallery.Services.Authentication;

#nullable enable

namespace NuGetGallery
{
    public class CreateTokenRequest
    {
        public string? Username { get; set; }

        public string? TokenType { get; set; }
    }

    public class TokenApiController : AppController
    {
        public static readonly string ControllerName = nameof(TokenApiController).Replace("Controller", string.Empty);
        private const string JsonContentType = "application/json";
        private const string ApiKeyTokenType = "ApiKey";
        private const string BearerScheme = "Bearer";
        private const string BearerPrefix = $"{BearerScheme} ";
        private const string AuthorizationHeaderName = "Authorization";

        private readonly IFederatedCredentialService _federatedCredentialService;
        private readonly IFederatedCredentialConfiguration _configuration;

        public TokenApiController(
            IFederatedCredentialService federatedCredentialService,
            IFederatedCredentialConfiguration configuration)
        {
            _federatedCredentialService = federatedCredentialService ?? throw new ArgumentNullException(nameof(federatedCredentialService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

#pragma warning disable CA3147 // No need to validate Antiforgery Token with API request
        [HttpPost]
        [ActionName(RouteName.CreateToken)]
        [AllowAnonymous] // authentication is handled inside the action
        public async Task<ActionResult> CreateToken(CreateTokenRequest request)
#pragma warning restore CA3147 // No need to validate Antiforgery Token with API request
        {
            if (!_configuration.EnableTokenApi)
            {
                return HttpNotFound();
            }

            if (!TryGetBearerToken(Request.Headers, out var bearerToken, out var errorMessage))
            {
                return UnauthorizedJson(errorMessage!);
            }

            if (User.Identity.IsAuthenticated)
            {
                return UnauthorizedJson("Only Bearer token authentication is accepted.");
            }

            if (!MediaTypeWithQualityHeaderValue.TryParse(Request.ContentType, out var parsed)
                || !string.Equals(parsed.MediaType, JsonContentType, StringComparison.OrdinalIgnoreCase))
            {
                return ErrorJson(HttpStatusCode.UnsupportedMediaType, $"The request must have a Content-Type of '{JsonContentType}'.");
            }

            if (string.IsNullOrWhiteSpace(Request.UserAgent))
            {
                return ErrorJson(HttpStatusCode.BadRequest, "A User-Agent header is required.");
            }

            if (string.IsNullOrWhiteSpace(request?.Username))
            {
                return ErrorJson(HttpStatusCode.BadRequest, "The username property in the request body is required.");
            }

            if (request?.TokenType != ApiKeyTokenType)
            {
                return ErrorJson(HttpStatusCode.BadRequest, $"The tokenType property in the request body is required and must set to '{ApiKeyTokenType}'.");
            }

            var result = await _federatedCredentialService.GenerateApiKeyAsync(request!.Username!, bearerToken!, Request.Headers);

            return result.Type switch
            {
                GenerateApiKeyResultType.BadRequest => ErrorJson(HttpStatusCode.BadRequest, result.UserMessage),
                GenerateApiKeyResultType.Unauthorized => UnauthorizedJson(result.UserMessage),
                GenerateApiKeyResultType.Created => ApiKeyJson(result),
                _ => throw new NotImplementedException($"Unexpected result type: {result.Type}"),
            };
        }

        private JsonResult ApiKeyJson(GenerateApiKeyResult result)
        {
            return Json(HttpStatusCode.OK, new
            {
                tokenType = ApiKeyTokenType,
                expires = result.Expires.ToString("O"),
                apiKey = result.PlaintextApiKey,
            });
        }

        private JsonResult UnauthorizedJson(string errorMessage)
        {
            // Add the "Federated" challenge so the other authentication providers (such as the default sign-in) are not triggered.
            OwinContext.Authentication.Challenge(AuthenticationTypes.Federated);

            Response.Headers["WWW-Authenticate"] = BearerScheme;

            return ErrorJson(HttpStatusCode.Unauthorized, errorMessage);
        }

        private JsonResult ErrorJson(HttpStatusCode status, string errorMessage)
        {
            // Show the error message in the HTTP reason phrase (status description) for compatibility with NuGet client error "protocol".
            // This, and the response body below, could be formalized with https://github.com/NuGet/NuGetGallery/issues/5818
            Response.StatusDescription = errorMessage;

            return Json(status, new { error = errorMessage });
        }

        private static bool TryGetBearerToken(NameValueCollection requestHeaders, out string? bearerToken, out string? errorMessage)
        {
            var authorizationHeaders = requestHeaders.GetValues(AuthorizationHeaderName);
            if (authorizationHeaders is null || authorizationHeaders.Length == 0)
            {
                bearerToken = null;
                errorMessage = $"The {AuthorizationHeaderName} header is missing.";
                return false;
            }

            if (authorizationHeaders.Length > 1)
            {
                bearerToken = null;
                errorMessage = $"Only one {AuthorizationHeaderName} header is allowed.";
                return false;
            }

            var authorizationHeader = authorizationHeaders[0];
            if (!authorizationHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                bearerToken = null;
                errorMessage = $"The {AuthorizationHeaderName} header value must start with '{BearerPrefix}'.";
                return false;
            }

            const string missingToken = $"The bearer token is missing from the {AuthorizationHeaderName} header.";

            if (authorizationHeader.Length <= BearerPrefix.Length)
            {
                bearerToken = null;
                errorMessage = missingToken;
                return false;
            }

            bearerToken = authorizationHeader.Substring(BearerPrefix.Length);
            if (string.IsNullOrWhiteSpace(bearerToken))
            {
                bearerToken = null;
                errorMessage = missingToken;
                return false;
            }

            bearerToken = bearerToken.Trim();
            errorMessage = null;
            return true;
        }
    }
}
