// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace NuGet.Services.AzureManagement
{
    public class AzureManagementAPIWrapper : IAzureManagementAPIWrapper
    {
        private const string AuthorityTemplate = "https://login.microsoftonline.com/{0}";
        private const string Resource = "https://management.core.windows.net/";
        private const int RenewTokenPriorToExpirationMinutes = 5;

        private readonly ClientCredential _clientCredential;
        private readonly string _authority;

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

            if (string.IsNullOrEmpty(configuration.AadTenant))
            {
                throw new ArgumentException(nameof(configuration.AadTenant));
            }


            _clientCredential = new ClientCredential(configuration.ClientId, configuration.ClientSecret);
            _authority = string.Format(CultureInfo.InvariantCulture, AuthorityTemplate, configuration.AadTenant);
        }

        public async Task RebootCloudServiceRoleInstanceAsync(
            string subscription,
            string resourceGroup,
            string name,
            string slot,
            string role,
            string roleInstance,
            CancellationToken token)
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

            if (string.IsNullOrEmpty(role))
            {
                throw new ArgumentException(nameof(role));
            }

            if (string.IsNullOrEmpty(roleInstance))
            {
                throw new ArgumentException(nameof(roleInstance));
            }

            var requestUrl = "https://management.azure.com" +
                $"/subscriptions/{subscription}" +
                $"/resourceGroups/{resourceGroup}" +
                $"/providers/Microsoft.ClassicCompute/domainNames/{name}" +
                $"/slots/{slot}" +
                $"/roles/{role}" +
                $"/roleInstances/{roleInstance}" +
                "/restart" +
                "?api-version=2015-06-01";

            await MakeAzureRequest(HttpMethod.Post, requestUrl, token);
        }

        public async Task<string> GetCloudServicePropertiesAsync(
            string subscription,
            string resourceGroup,
            string name,
            string slot,
            CancellationToken token)
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

            var requestUrl = "https://management.azure.com" +
                $"/subscriptions/{subscription}" +
                $"/resourceGroups/{resourceGroup}" +
                $"/providers/Microsoft.ClassicCompute/domainNames/{name}" +
                $"/slots/{slot}" +
                "?api-version=2016-11-01";

            return await MakeAzureRequest(HttpMethod.Get, requestUrl, token);
        }

        public async Task<string> GetTrafficManagerPropertiesAsync(
            string subscription,
            string resourceGroup,
            string profileName,
            CancellationToken token)
        {
            if (string.IsNullOrEmpty(subscription))
            {
                throw new ArgumentException(nameof(subscription));
            }

            if (string.IsNullOrEmpty(resourceGroup))
            {
                throw new ArgumentException(nameof(resourceGroup));
            }

            if (string.IsNullOrEmpty(profileName))
            {
                throw new ArgumentException(nameof(profileName));
            }

            var requestUrl = "https://management.azure.com" +
                $"/subscriptions/{subscription}" +
                $"/resourceGroups/{resourceGroup}" +
                $"/providers/Microsoft.Network/trafficmanagerprofiles/{profileName}" +
                "?api-version=2017-05-01";

            return await MakeAzureRequest(HttpMethod.Get, requestUrl, token);
        }

        private async Task<string> MakeAzureRequest(HttpMethod method, string requestUrl, CancellationToken token)
        {
            await RenewAccessToken();

            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage(method, requestUrl))
            {
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

                    throw new AzureManagementException("Failed to make request to Azure." +
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
                var context = new AuthenticationContext(_authority, validateAuthority: false);
                AuthenticationResult authenticationResult = await context.AcquireTokenAsync(Resource, _clientCredential);
                return authenticationResult;
            }
            catch (AdalException adalException)
            {
                throw new AzureManagementException($"Failed to create token. Client id: {_clientCredential.ClientId}, Authority: {_authority}", adalException);
            }
        }
    }
}
