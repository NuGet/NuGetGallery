// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.GitHub.Configuration;
using NuGet.Services.KeyVault;

namespace NuGet.Services.GitHub.Authentication
{
    public class GitHubAppAuthProvider : IGitHubAuthProvider
    {
        private readonly string _gitHubAppId;
        private readonly string _gitHubInstallation;
        private readonly IKeyVaultDataSigner _dataSigner;

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
            _dataSigner = dataSigner ?? throw new ArgumentNullException(nameof(dataSigner));
        }

        public async Task AddAuthentication(HttpRequestMessage message)
        {
            string unsignedJwt = CreateJwt();
            byte[] digest = GetHash(unsignedJwt);
            byte[] signature = await _dataSigner.SignDataAsync(digest, KeyVaultSignatureAlgorithm.RS256);
            string jwt = $"{unsignedJwt}.{Base64UrlEncode(signature)}";
            string installationId = await GetInstallationId(jwt);
            string authToken = await GetAuthToken(jwt, installationId);

            throw new NotImplementedException();
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
