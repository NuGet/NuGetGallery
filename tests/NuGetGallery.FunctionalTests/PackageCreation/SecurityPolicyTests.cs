// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.PackageCreation
{
    public class SecurityPolicyTests : GalleryTestBase
    {
        private readonly ClientSdkHelper _clientSdkHelper;

        public SecurityPolicyTests(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
            _clientSdkHelper = new ClientSdkHelper(TestOutputHelper);
        }
        
        [Theory(Skip = "Depends on TestSecurityPoliciesAccountApiKey account setup")]
        [InlineData("")]
        [InlineData("3.5.0")]
        [InlineData("4.1.0-beta")]
        [Description("Package push fails if min client version policy not met")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task PackagePushReturns400_RequireMinClientVersionPolicyNotMet(string clientVersion)
        {
            // Arrange
            var id = $"{nameof(PackagePushReturns400_RequireMinClientVersionPolicyNotMet)}.{DateTime.UtcNow.Ticks}";

            // Act
            var response = await PushPackageAsync(EnvironmentSettings.TestSecurityPoliciesAccountApiKey, id, clientVersion);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory(Skip = "Depends on TestSecurityPoliciesAccountApiKey account setup")]
        [InlineData("4.1.0")]
        [InlineData("4.3.0-beta")]
        [Description("Package push succeeds if min client version policy met")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task PackagePushReturns200_RequireMinClientVersionPolicyMet(string clientVersion)
        {
            // Arrange
            var id = $"{nameof(PackagePushReturns200_RequireMinClientVersionPolicyMet)}.{DateTime.UtcNow.Ticks}";

            // Act
            var response = await PushPackageAsync(EnvironmentSettings.TestSecurityPoliciesAccountApiKey, id, clientVersion);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact(Skip = "Depends on TestSecurityPoliciesAccountApiKey account setup")]
        [Description("VerifyPackageKey fails if package verify policy not met")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task VerifyPackageKeyReturns400_RequirePackageVerifyScopePolicyNotMet()
        {
            // Arrange
            var id = $"{nameof(VerifyPackageKeyReturns400_RequirePackageVerifyScopePolicyNotMet)}.{DateTime.UtcNow.Ticks}";

            var pushResponse = await PushPackageAsync(EnvironmentSettings.TestSecurityPoliciesAccountApiKey, id, "4.1.0");
            Assert.Equal(HttpStatusCode.Created, pushResponse.StatusCode);

            // Act
            var verifyResponse = await VerifyPackageKey(EnvironmentSettings.TestSecurityPoliciesAccountApiKey, id);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, verifyResponse);
        }

        [Fact(Skip = "Depends on TestSecurityPoliciesAccountApiKey account setup")]
        [Description("VerifyPackageKey succeeds if package verify policy met")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task VerifyPackageKeyReturns200_RequirePackageVerifyScopePolicyMet()
        {
            // Arrange
            var id = $"{nameof(VerifyPackageKeyReturns200_RequirePackageVerifyScopePolicyMet)}.{DateTime.UtcNow.Ticks}";

            var pushResponse = await PushPackageAsync(EnvironmentSettings.TestSecurityPoliciesAccountApiKey, id, "4.1.0");
            Assert.Equal(HttpStatusCode.Created, pushResponse.StatusCode);

            var verifyKey = await CreateVerificationKey(EnvironmentSettings.TestSecurityPoliciesAccountApiKey, id);

            // Act
            var verifyResponse = await VerifyPackageKey(verifyKey, id);

            // Assert
            Assert.Equal(HttpStatusCode.OK, verifyResponse);
        }

        [Fact]
        [Description("VerifyPackageKey fails if package isn't found.")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task VerifyPackageKeyReturns404ForMissingPackage()
        {
            Assert.Equal(HttpStatusCode.NotFound, await VerifyPackageKey(EnvironmentSettings.TestAccountApiKey, "VerifyPackageKeyReturns404ForMissingPackage", "1.0.0"));
        }

        [Fact]
        [Description("VerifyPackageKey succeeds for full API key without deletion.")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task VerifyPackageKeyReturns200ForFullApiKey()
        {
            // Arrange
            var packageId = $"VerifyPackageKeyReturns200ForFullApiKey.{DateTimeOffset.UtcNow.Ticks}";
            var packageVersion = "1.0.0";

            await _clientSdkHelper.UploadNewPackage(packageId, packageVersion);

            // Act & Assert
            Assert.Equal(HttpStatusCode.OK, await VerifyPackageKey(EnvironmentSettings.TestAccountApiKey, packageId));
            Assert.Equal(HttpStatusCode.OK, await VerifyPackageKey(EnvironmentSettings.TestAccountApiKey, packageId, packageVersion));
        }

        [Fact]
        [Description("VerifyPackageKey succeeds for temp API key with deletion.")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task VerifyPackageKeyReturns200ForTempApiKey()
        {
            // Arrange
            var packageId = $"VerifyPackageKeySupportsFullAndTempApiKeys.{DateTimeOffset.UtcNow.Ticks}";
            var packageVersion = "1.0.0";

            await _clientSdkHelper.UploadNewPackage(packageId, packageVersion);

            var verificationKey = await CreateVerificationKey(EnvironmentSettings.TestAccountApiKey, packageId, packageVersion);

            // Act & Assert
            Assert.Equal(HttpStatusCode.OK, await VerifyPackageKey(verificationKey, packageId, packageVersion));
            Assert.Equal(HttpStatusCode.Forbidden, await VerifyPackageKey(verificationKey, packageId, packageVersion));
        }

        private async Task<HttpResponseMessage> PushPackageAsync(string apiKey, string packageId, string clientVersion = null)
        {
            var packageCreationHelper = new PackageCreationHelper(TestOutputHelper);
            var packagePath = await packageCreationHelper.CreatePackage(packageId);

            using (var httpClient = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Put, UrlHelper.V2FeedPushSourceUrl))
            {
                request.Headers.Add(Constants.NuGetHeaderApiKey, EnvironmentSettings.TestSecurityPoliciesAccountApiKey);
                if (clientVersion != null)
                {
                    request.Headers.Add(Constants.NuGetHeaderClientVersion, clientVersion);
                }
                request.Content = new StreamContent(new FileStream(packagePath, FileMode.Open));

                return await httpClient.SendAsync(request);
            }
        }

        private async Task<string> CreateVerificationKey(string apiKey, string packageId, string packageVersion = null)
        {
            var route = string.IsNullOrWhiteSpace(packageVersion) ?
                $"package/create-verification-key/{packageId}" :
                $"package/create-verification-key/{packageId}/{packageVersion}";

            var request = WebRequest.Create(UrlHelper.V2FeedRootUrl + route);
            request.Method = "POST";
            request.ContentLength = 0;
            request.Headers.Add(Constants.NuGetHeaderApiKey, apiKey);
            request.Headers.Add(Constants.NuGetHeaderClientVersion, "NuGetGallery.FunctionalTests");

            var response = await request.GetResponseAsync() as HttpWebResponse;
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            string responseText;
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseText = await sr.ReadToEndAsync();
            }

            var json = JObject.Parse(responseText);
            var expiration = json.Value<DateTime>("Expires");

            // Verification key should expire in 1 day. Ensure expiration is within 2 days in case client/server clocks differ.
            Assert.True(expiration - DateTime.UtcNow < TimeSpan.FromDays(2), "Verification keys should expire after 1 day.");

            return json.Value<string>("Key");
        }

        private async Task<HttpStatusCode> VerifyPackageKey(string apiKey, string packageId, string packageVersion = null)
        {
            var route = string.IsNullOrWhiteSpace(packageVersion) ?
                $"verifykey/{packageId}" :
                $"verifykey/{packageId}/{packageVersion}";

            var request = WebRequest.Create(UrlHelper.V2FeedRootUrl + route);
            request.Headers.Add(Constants.NuGetHeaderApiKey, apiKey);

            try
            {
                var response = await request.GetResponseAsync() as HttpWebResponse;
                return response.StatusCode;
            }
            catch (WebException e)
            {
                return ((HttpWebResponse)e.Response).StatusCode;
            }
        }
    }
}
