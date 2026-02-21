// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using NuGet.Services.GitHub.Configuration;
using NuGet.Services.KeyVault;
using Octokit;

namespace NuGet.Services.GitHub.Authentication
{
    public class GitHubAppAuthProvider : IGitHubAuthProvider
    {
        private readonly string _gitHubAppId;
        private readonly string _gitHubInstallation;
        private readonly string _userAgent;
        private readonly IKeyVaultDataSigner _dataSigner;
        private long _installationId = -1;
        private AccessToken _accessToken = null;

        public GitHubAppAuthProvider(
            GraphQLQueryConfiguration configuration,
            IKeyVaultDataSigner dataSigner)
        {
            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            _gitHubAppId = configuration.GitHubAppId ?? throw new ArgumentException("GitHub App Id must be provided.", nameof(configuration));
            _gitHubInstallation = configuration.GitHubInstallationName ?? throw new ArgumentException("GitHub Installation Name must be provided.", nameof(configuration));
            _userAgent = configuration.UserAgent ?? throw new ArgumentException("User Agent must be provided.", nameof(configuration));
            _dataSigner = dataSigner ?? throw new ArgumentNullException(nameof(dataSigner));
        }

        public async Task AddAuthentication(HttpRequestMessage message)
        {
            string authToken = await GetAuthTokenAsync();

            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
        }

        private async Task<string> CreateSignedJwt()
        {
            string unsignedJwt = CreateJwt();
            byte[] digest = GetHash(unsignedJwt);
            byte[] signature = await _dataSigner.SignDataAsync(digest, KeyVaultSignatureAlgorithm.RS256);
            string jwt = $"{unsignedJwt}.{Base64UrlEncode(signature)}";
            return jwt;
        }

        private byte[] GetHash(string unsignedJwt)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            return sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(unsignedJwt));
        }

        private static string Base64UrlEncode(byte[] signature)
        {
            return MakeBase64UrlSafe(Convert.ToBase64String(signature));
        }

        private static string Base64UrlEncode(string str)
        {
            return Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(str));
        }

        private static string MakeBase64UrlSafe(string base64)
        {
            return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private string CreateJwt()
        {
            var header = new JwtHeader();
            var payload = new JwtPayload
            {
                Iss = _gitHubAppId,
            };

			var serializerOptions = new JsonSerializerOptions
			{
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase
			};
			string headerJson = JsonSerializer.Serialize(header, serializerOptions);
			string payloadJson = JsonSerializer.Serialize(payload, serializerOptions);

            string headerBase64 = Base64UrlEncode(headerJson);
            string payloadBase64 = Base64UrlEncode(payloadJson);

            return $"{headerBase64}.{payloadBase64}";
        }

        private async Task<long> GetInstallationIdAsync(GitHubClient client)
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            IReadOnlyList<Installation> installations = await client.GitHubApps.GetAllInstallationsForCurrent();

            foreach (var installation in installations)
            {
                if (installation.Account.Login.Equals(_gitHubInstallation, StringComparison.OrdinalIgnoreCase))
                {
                    return installation.Id;
                }
            }

            throw new InvalidOperationException($"GitHub installation '{_gitHubInstallation}' not found.");
        }

        private async Task<string> GetAuthTokenAsync()
        {
            if (_accessToken is null || _accessToken.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(2))
            {
                var jwt = await CreateSignedJwt();
                GitHubClient client = CreateGitHubClient(jwt);

                if (_installationId == -1)
                {
                    _installationId = await GetInstallationIdAsync(client);
                }

                _accessToken = await client.GitHubApps.CreateInstallationToken(_installationId);
            }

			return _accessToken.Token;
        }

        private GitHubClient CreateGitHubClient(string jwt)
        {
            return new GitHubClient(new Octokit.ProductHeaderValue(_userAgent))
            {
                Credentials = new Credentials(jwt, AuthenticationType.Bearer)
            };
        }

        private class JwtHeader
        {
            public string Alg { get; set; } = "RS256";
            public string Typ { get; set; } = "JWT";
        }

        private class JwtPayload
        {
            public string Iss { get; set; }
            public long Iat { get; set; } = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds();
            public long Exp { get; set; } = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        }
    }
}
