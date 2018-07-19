// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace NuGet.Services.Sql
{
    internal class AccessTokenCache
    {
        private const string AzureSqlResourceTokenUrl = "https://database.windows.net/";

        private const double DefaultMinExpirationInMinutes = 30;

        private ConcurrentDictionary<string, AccessTokenCacheValue> _cache = new ConcurrentDictionary<string, AccessTokenCacheValue>();

        private SemaphoreSlim AcquireTokenLock = new SemaphoreSlim(1, 1);

        private const int ForegroundRefreshTimeoutMilliseconds = 6000;

        private const int BackgroundRefreshTimeoutMilliseconds = 250;

        public async Task<IAuthenticationResult> GetAsync(
            AzureSqlConnectionStringBuilder connectionString,
            string clientCertificateData,
            ILogger logger = null)
        {
            AccessTokenCacheValue accessToken;

            if (TryGetValue(connectionString, out accessToken) && !IsExpired(accessToken))
            {
                // Refresh access token in background, if near expiry or client certificate has changed.
                if (IsNearExpiry(accessToken) || ClientCertificateHasChanged(accessToken, clientCertificateData))
                {
                    TriggerBackgroundRefresh(connectionString, clientCertificateData, logger);
                }

                // Returned cached access token.
                return accessToken.AuthenticationResult;
            }

            // Acquire token in foreground, first time or if already expired.
            if (await TryRefreshAccessTokenAsync(connectionString, clientCertificateData, logger, ForegroundRefreshTimeoutMilliseconds))
            {
                if (TryGetValue(connectionString, out accessToken))
                {
                    return accessToken.AuthenticationResult;
                }
            }

            throw new InvalidOperationException($"Failed to acquire access token for {connectionString.Sql.InitialCatalog}.");
        }

        /// <summary>
        /// Access token has expired, and must be refreshed.
        /// </summary>
        private bool IsExpired(AccessTokenCacheValue accessToken)
        {
            return TokenExpiresIn(accessToken.AuthenticationResult, expirationInMinutes: 5);
        }
        
        /// <summary>
        /// Access token is near expiration, and should be refreshed soon.
        /// </summary>
        private bool IsNearExpiry(AccessTokenCacheValue accessToken)
        {
            return TokenExpiresIn(accessToken.AuthenticationResult, DefaultMinExpirationInMinutes);
        }
        
        /// <summary>
        /// KeyVault certificate has been rotated, and client assertion and tokens should be refreshed soon. Old certificate
        /// (and tokens) should still be valid, so long as the rotation policy is not set to 100% of certificate lifetime.
        /// </summary>
        private bool ClientCertificateHasChanged(
            AccessTokenCacheValue accessToken,
            string clientCertificateData)
        {
            return !accessToken.ClientCertificateData.Equals(clientCertificateData, StringComparison.InvariantCulture);
        }

        private bool TokenExpiresIn(IAuthenticationResult token, double expirationInMinutes)
        {
            return (token.ExpiresOn - DateTimeOffset.Now).TotalMinutes < expirationInMinutes;
        }

        protected virtual bool TryGetValue(
            AzureSqlConnectionStringBuilder connectionString,
            out AccessTokenCacheValue accessToken)
        {
            return _cache.TryGetValue(connectionString.ConnectionString, out accessToken);
        }

        /// <summary>
        /// Attempt non-blocking refresh of access token in background with retries.
        /// </summary>
        private void TriggerBackgroundRefresh(
            AzureSqlConnectionStringBuilder connectionString,
            string clientCertificateData,
            ILogger logger)
        {
            Task.Run(async () =>
            {
                await TryRefreshAccessTokenAsync(connectionString, clientCertificateData, logger,
                    refreshTimeoutMilliseconds: BackgroundRefreshTimeoutMilliseconds);
            });
        }

        /// <summary>
        /// Try to refresh the access token in the token cache.
        /// </summary>
        private async Task<bool> TryRefreshAccessTokenAsync(
            AzureSqlConnectionStringBuilder connectionString,
            string clientCertificateData,
            ILogger logger,
            int refreshTimeoutMilliseconds)
        {
            if (!await AcquireTokenLock.WaitAsync(refreshTimeoutMilliseconds))
            {
                return false;
            }

            try
            {
                TryGetValue(connectionString, out var accessToken);
                if (accessToken == null || IsNearExpiry(accessToken)
                    || ClientCertificateHasChanged(accessToken, clientCertificateData))
                {
                    return await RefreshAccessTokenAsync(connectionString, clientCertificateData, logger);
                }
            }
            finally
            {
                AcquireTokenLock.Release();
            }

            return true;
        }

        /// <summary>
        /// Refresh the access token in the token cache.
        /// </summary>
        private async Task<bool> RefreshAccessTokenAsync(
            AzureSqlConnectionStringBuilder connectionString,
            string clientCertificateData,
            ILogger logger)
        {
            try
            {
                var start = DateTimeOffset.Now;
                var accessToken = await AcquireAccessTokenAsync(connectionString, clientCertificateData);

                Debug.Assert(accessToken != null);

                logger?.LogInformation("Refreshed access token for {InitialCatalog} in {ElapsedMilliseconds}.",
                    connectionString.Sql.InitialCatalog,
                    (DateTimeOffset.Now - start).TotalMilliseconds);

                _cache.AddOrUpdate(connectionString.ConnectionString, accessToken, (k, v) => accessToken);

                return true;
            }
            catch (Exception ex)
            {
                logger?.LogError(0, ex, "Failed to refresh access token for {InitialCatalog}.",
                    connectionString.Sql.InitialCatalog);

                return false;
            }
        }

        /// <summary>
        /// Request an access token from the token service.
        /// </summary>
        protected virtual async Task<AccessTokenCacheValue> AcquireAccessTokenAsync(
            AzureSqlConnectionStringBuilder connectionString,
            string clientCertificateData)
        {
            using (var certificate = new X509Certificate2(Convert.FromBase64String(clientCertificateData), string.Empty))
            {
                var clientAssertion = new ClientAssertionCertificate(connectionString.AadClientId, certificate);
                var authContext = new AuthenticationContext(connectionString.AadAuthority, tokenCache: null);

                var authResult = await authContext.AcquireTokenAsync(AzureSqlResourceTokenUrl, clientAssertion, connectionString.AadSendX5c);

                return new AccessTokenCacheValue(clientCertificateData, authResult);
            }
        }
    }
}
