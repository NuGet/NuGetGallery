// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGetGallery.FunctionalTests.Helpers;
using NuGetGallery.FunctionalTests.XunitExtensions;
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
        
        [DefaultSecurityPoliciesEnforcedTheory]
        [DefaultSecurityPoliciesEnforcedData("")]
        [DefaultSecurityPoliciesEnforcedData("3.5.0")]
        [DefaultSecurityPoliciesEnforcedData("4.1.0-beta")]
        [Description("Package push fails if min client version policy not met")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task PackagePush_Returns400IfMinClientVersionPolicyNotMet(string clientVersion)
        {
            // Arrange
            var id = $"ValidClientVersion{Guid.NewGuid():N}";

            // Act
            var response = await PushPackageAsync(GalleryConfiguration.Instance.Account.ApiKey, id, clientVersion);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [DefaultSecurityPoliciesEnforcedTheory]
        [DefaultSecurityPoliciesEnforcedData("4.1.0")]
        [DefaultSecurityPoliciesEnforcedData("4.3.0-beta")]
        [Description("Package push succeeds if min client version policy met")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task PackagePush_Returns200IfMinClientVersionPolicyMet(string clientVersion)
        {
            // Arrange
            var id = $"ClientVersionTooLow{Guid.NewGuid():N}";

            // Act
            var response = await PushPackageAsync(GalleryConfiguration.Instance.Account.ApiKey, id, clientVersion);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [DefaultSecurityPoliciesEnforcedFact]
        [Description("Package push succeeds if min protocol version policy met")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task PackagePush_Returns200IfMinProtocolVersionPolicyMet()
        {
            // Arrange
            var id = $"ValidProtocolVersion{Guid.NewGuid():N}";

            // Act
            var response = await PushPackageAsync(GalleryConfiguration.Instance.Account.ApiKey, id, clientVersion: null, protocolVersion: "4.1.0");

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [DefaultSecurityPoliciesEnforcedFact]
        [Description("VerifyPackageKey fails if package verify policy not met")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task VerifyPackageKey_Returns400IfPackageVerifyScopePolicyNotMet()
        {
            // Arrange
            var id = $"InvalidScopeForVerify{Guid.NewGuid():N}";

            var pushResponse = await PushPackageAsync(GalleryConfiguration.Instance.Account.ApiKey, id, "4.1.0");
            Assert.Equal(HttpStatusCode.Created, pushResponse.StatusCode);

            // Act
            var verifyResponse = await VerifyPackageKey(GalleryConfiguration.Instance.Account.ApiKey, id);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, verifyResponse);
        }

        [DefaultSecurityPoliciesEnforcedFact]
        [Description("VerifyPackageKey succeeds if package verify policy met")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task VerifyPackageKey_Returns200IfPackageVerifyScopePolicyMet()
        {
            // Arrange
            var id = $"ValidScopeForVerify{Guid.NewGuid():N}";

            var pushResponse = await PushPackageAsync(GalleryConfiguration.Instance.Account.ApiKey, id, "4.1.0");
            Assert.Equal(HttpStatusCode.Created, pushResponse.StatusCode);

            var verifyKey = await CreateVerificationKey(GalleryConfiguration.Instance.Account.ApiKey, id);

            // Act
            var verifyResponse = await VerifyPackageKey(verifyKey, id);

            // Assert
            Assert.Equal(HttpStatusCode.OK, verifyResponse);
        }

        [DefaultSecurityPoliciesEnforcedFact]
        [Description("VerifyPackageKey fails if package isn't found.")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task VerifyPackageKey_Returns404ForMissingPackage()
        {
            // Arrange
            var packageInfo = await _clientSdkHelper.UploadPackage();
            var packageId = packageInfo.Id;
            var packageVersion = packageInfo.Version;
            var missingPackageId = UploadHelper.GetUniquePackageId();

            var verificationKey = await CreateVerificationKey(GalleryConfiguration.Instance.Account.ApiKey, packageId, packageVersion);
            
            // Act & Assert
            Assert.Equal(HttpStatusCode.NotFound, await VerifyPackageKey(verificationKey, missingPackageId, "1.0.0"));
        }

        [DefaultSecurityPoliciesEnforcedFact(runIfEnforced: false)]
        [Description("VerifyPackageKey succeeds for full API key without deletion.")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task VerifyPackageKey_Returns200ForFullApiKey()
        {
            // Arrange
            var packageInfo = await _clientSdkHelper.UploadPackage();
            var packageId = packageInfo.Id;
            var packageVersion = packageInfo.Version;

            // Act & Assert
            Assert.Equal(HttpStatusCode.OK, await VerifyPackageKey(GalleryConfiguration.Instance.Account.ApiKey, packageId));
            Assert.Equal(HttpStatusCode.OK, await VerifyPackageKey(GalleryConfiguration.Instance.Account.ApiKey, packageId, packageVersion));
        }

        [Fact]
        [Description("VerifyPackageKey succeeds for temp API key with deletion.")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task VerifyPackageKey_Returns200ForTempApiKey()
        {
            // Arrange
            var packageInfo = await _clientSdkHelper.UploadPackage();
            var packageId = packageInfo.Id;
            var packageVersion = packageInfo.Version;

            var verificationKey = await CreateVerificationKey(GalleryConfiguration.Instance.Account.ApiKey, packageId, packageVersion);

            // Act & Assert
            Assert.Equal(HttpStatusCode.OK, await VerifyPackageKey(verificationKey, packageId, packageVersion));
            Assert.Equal(HttpStatusCode.Forbidden, await VerifyPackageKey(verificationKey, packageId, packageVersion));
        }

        private async Task<HttpResponseMessage> PushPackageAsync(string apiKey, string packageId, string clientVersion = null, string protocolVersion = null)
        {
            var packageCreationHelper = new PackageCreationHelper(TestOutputHelper);
            var packagePath = await packageCreationHelper.CreatePackage(packageId);

            using (var httpClient = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Put, UrlHelper.V2FeedPushSourceUrl))
            {
                request.Headers.Add(Constants.NuGetHeaderApiKey, GalleryConfiguration.Instance.Account.ApiKey);
                if (clientVersion != null)
                {
                    request.Headers.Add(Constants.NuGetHeaderClientVersion, clientVersion);
                }
                if (protocolVersion != null)
                {
                    request.Headers.Add(Constants.NuGetHeaderProtocolVersion, protocolVersion); 
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

            string responseText;
            using (var request = new HttpRequestMessage(HttpMethod.Post, UrlHelper.V2FeedRootUrl + route))
            {
                request.Headers.Add(Constants.NuGetHeaderApiKey, apiKey);
                request.Headers.Add(Constants.NuGetHeaderClientVersion, "NuGetGallery.FunctionalTests");

                using (var httpClient = new HttpClient())
                using (var response = await httpClient.SendAsync(request))
                {
                    Assert.NotNull(response);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                    responseText = await response.Content.ReadAsStringAsync();
                }
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

            using (var request = new HttpRequestMessage())
            {
                request.RequestUri = new Uri(UrlHelper.V2FeedRootUrl + route);
                request.Headers.Add(Constants.NuGetHeaderApiKey, apiKey);

                using (var httpClient = new HttpClient())
                using (var response = await httpClient.SendAsync(request))
                {
                    return response.StatusCode;
                }
            }
        }
    }
}
