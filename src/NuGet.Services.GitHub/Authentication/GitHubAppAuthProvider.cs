// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
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
        private readonly SemaphoreSlim _tokenRefreshLock = new(1, 1);

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

		private async Task<string> CreateSignedJwtAsync()
		{
			string unsignedJwt = CreateJwt();
			byte[] data = System.Text.Encoding.UTF8.GetBytes(unsignedJwt);
			byte[] signature = await _dataSigner.SignDataAsync(data, KeyVaultSignatureAlgorithm.RS256);
			return $"{unsignedJwt}.{Base64UrlEncoder.Encode(signature)}";
		}

		private string CreateJwt()
		{
			var now = DateTimeOffset.UtcNow;

			var header = new JwtHeader();
			header[JwtHeaderParameterNames.Alg] = SecurityAlgorithms.RsaSha256;
			header[JwtHeaderParameterNames.Typ] = "JWT";

			var payload = new JwtPayload(
				issuer: _gitHubAppId,
				audience: null,
				claims: null,
				notBefore: null,
				expires: now.AddMinutes(5).UtcDateTime,
				issuedAt: now.AddMinutes(-1).UtcDateTime);

			return $"{header.Base64UrlEncode()}.{payload.Base64UrlEncode()}";
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
                await _tokenRefreshLock.WaitAsync();
                try
                {
                    if (_accessToken is null || _accessToken.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(2))
                    {
                        var jwt = await CreateSignedJwtAsync();
                        GitHubClient client = CreateGitHubClient(jwt);

                        if (_installationId == -1)
                        {
                            _installationId = await GetInstallationIdAsync(client);
                        }

                        _accessToken = await client.GitHubApps.CreateInstallationToken(_installationId);
                    }
                }
                finally
                {
                    _tokenRefreshLock.Release();
                }
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

            }
        }
