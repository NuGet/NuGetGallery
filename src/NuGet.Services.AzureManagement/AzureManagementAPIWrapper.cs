// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace NuGet.Services.AzureManagement
{
    public class AzureManagementAPIWrapper : IAzureManagementAPIWrapper
    {
        private const string Authority = @"https://login.microsoftonline.com/microsoft.onmicrosoft.com";
        private const string Resource = "https://management.core.windows.net/";
        private const int RenewTokenPriorToExpirationMinutes = 5;

        private string _clientId;
        private string _clientSecret;

        private string _accessToken;
        private DateTimeOffset _tokenExpirationTime;

        public AzureManagementAPIWrapper(IAzureManagementAPIWrapperConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (string.IsNullOrEmpty(configuration.ClientId))
            {
                throw new ArgumentException(nameof(configuration.ClientId));
            }

            if (string.IsNullOrEmpty(configuration.ClientSecret))
            {
                throw new ArgumentException(nameof(configuration.ClientSecret));
            }

            _clientId = configuration.ClientId;
            _clientSecret = configuration.ClientSecret;
        }

        public async Task<string> GetCloudServicePropertiesAsync(string subscription, string resourceGroup, string name, string slot, CancellationToken token)
        {
            if (string.IsNullOrEmpty(subscription))
            {
                throw new ArgumentException(nameof(subscription));
            }

            if (string.IsNullOrEmpty(resourceGroup))
            {
                throw new ArgumentException(nameof(resourceGroup));
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(nameof(name));
            }

            if (string.IsNullOrEmpty(slot))
            {
                throw new ArgumentException(nameof(slot));
            }

            await RenewAccessToken();

            const string RequestUrlFormat = @"https://management.azure.com/subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.ClassicCompute/domainNames/{2}/slots/{3}?api-version=2016-11-01";

            string requestUrl = string.Format(RequestUrlFormat, subscription, resourceGroup, name, slot);

            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                HttpResponseMessage response = await client.SendAsync(request, token);

                if (response.IsSuccessStatusCode)
                {
                    string result = await response.Content.ReadAsStringAsync();
                    return result;
                }
                else
                {
                    string errorDetails = "Unknown";
                    try
                    {
                        // Try to read the response.. might work..
                        errorDetails = await response.Content.ReadAsStringAsync();
                    }
                    catch
                    {
                    }
                    
                    throw new AzureManagementException($"Failed to get cloud service properties." +
                        $" Url: {requestUrl}, Return code: {response.StatusCode} {response.ReasonPhrase}, Error: {errorDetails}");
                }
            }
        }

        private async Task RenewAccessToken()
        {
            if (string.IsNullOrEmpty(_accessToken) ||
                _tokenExpirationTime < DateTime.UtcNow + TimeSpan.FromMinutes(RenewTokenPriorToExpirationMinutes))
            {
                var authenticationResult = await GetAccessToken();
                _accessToken = authenticationResult.AccessToken;
                _tokenExpirationTime = authenticationResult.ExpiresOn;
            }
        }

        private async Task<AuthenticationResult> GetAccessToken()
        {
            try
            {
                var clientCredential = new ClientCredential(_clientId, _clientSecret);
                var context = new AuthenticationContext(Authority, validateAuthority: false);
                AuthenticationResult authenticationResult = await context.AcquireTokenAsync(Resource, clientCredential);
                return authenticationResult;
            }
            catch (AdalException adalException)
            {
                throw new AzureManagementException($"Failed to create token. Client id: {_clientId}", adalException);
            }
        }
    }
}
