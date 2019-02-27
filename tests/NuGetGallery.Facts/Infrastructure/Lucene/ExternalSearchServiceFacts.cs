// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Text;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using NuGet.Services.Search.Client;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace NuGetGallery.Infrastructure.Search
{
    public class ExternalSearchServiceFacts
    {
        public class TheGetClientMethod
        {
            private IAppConfiguration GetConfiguration()
            {
                var mockConfiguration = new Mock<IAppConfiguration>();
                mockConfiguration.SetupGet(c => c.ServiceDiscoveryUri).Returns(new Uri("https://api.nuget.org/v3/index.json"));
                mockConfiguration.SetupGet(c => c.SearchServiceResourceType).Returns("SearchGalleryQueryService/3.0.0-rc");
                return mockConfiguration.Object;
            }

            private IDiagnosticsService GetDiagnosticsService()
            {
                var mockConfiguration = new Mock<IDiagnosticsService>();
                mockConfiguration.Setup(ds => ds.GetSource(It.IsAny<string>()))
                    .Returns(Mock.Of<IDiagnosticsSource>());
                return mockConfiguration.Object;
            }

            private ILogger<ResilientSearchHttpClient> GetLogger()
            {
                var mockConfiguration = new Mock<ILogger<ResilientSearchHttpClient>>();
                return mockConfiguration.Object;
            }

            private IResilientSearchClient GetResilientSearchClient(string path, string queryString)
            {
                var content = new JObject(
                           new JProperty("queryString", queryString),
                           new JProperty("path", path));

                var responseMessage = new HttpResponseMessage()
                {
                    Content = new StringContent(content.ToString(), Encoding.UTF8, CoreConstants.TextContentType),
                    RequestMessage = new HttpRequestMessage(HttpMethod.Get, $"{path}/{queryString}"),
                    StatusCode = HttpStatusCode.OK
                };

                var mockIResilientSearchClient = new Mock<IResilientSearchClient>();
                mockIResilientSearchClient.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(responseMessage);

                return mockIResilientSearchClient.Object;
            }

            private ISearchClient GetSearchClient(string path, string queryString)
            {
                return new GallerySearchClient(GetResilientSearchClient(path, queryString));
            }

            [Fact]
            public void ReturnsTheExpectedClient()
            {
                // Arrange
                var service = new ExternalSearchService(GetConfiguration(), GetDiagnosticsService(), GetSearchClient(string.Empty, string.Empty));

                // Act
                var client = service.GetClient();
                var clientType = client.GetType();

                Assert.Equal("NuGet.Services.Search.Client.SearchClient", clientType.FullName);
            }
        }

        public class TheReadPackageMethod
        {
            [Fact]
            public void WhenSearchFilterIsNotSemVer2_DoesNotSetIsLatestSemVer2Properties()
            {
                var jObject = JObject.Parse(isLatestSearchResult);

                // Act
                var result = ExternalSearchService.ReadPackage(jObject, "1.0.0");

                // Assert
                Assert.False(result.IsLatestSemVer2);
                Assert.False(result.IsLatestStableSemVer2);
            }

            [Fact]
            public void WhenSearchFilterIsSemVer2_SetsIsLatestSemVer2Properties()
            {
                var jObject = JObject.Parse(isLatestSearchResult);

                // Act
                var result = ExternalSearchService.ReadPackage(jObject, SemVerLevelKey.SemVerLevel2);

                // Assert
                Assert.True(result.IsLatestSemVer2);
                Assert.True(result.IsLatestStableSemVer2);
            }

            public static string isLatestSearchResult = @"{
  ""PackageRegistration"": {
    ""Id"": ""MyPackage"",
    ""DownloadCount"": 1,
    ""Verified"": false,
    ""Owners"": [ ""owner"" ]
  },
  ""Version"": ""1.2.34+git.abc123"",
  ""NormalizedVersion"": ""1.2.34"",
  ""Title"": ""MyPackage"",
  ""Description"": """",
  ""Summary"": """",
  ""Authors"": ""author"",
  ""Copyright"": ""Copyright 2018"",
  ""Tags"": ""sometag"",
  ""ProjectUrl"": """",
  ""IconUrl"": """",
  ""IsLatestStable"": true,
  ""IsLatest"": true,
  ""Listed"": true,
  ""Created"": ""2018-01-01T01:00:00.000-00:00"",
  ""Published"": ""2018-01-01T01:00:00.000-00:00"",
  ""LastUpdated"": ""2018-01-01T01:00:00.000-00:00"",
  ""LastEdited"": ""2018-01-01T01:00:00.000-00:00"",
  ""DownloadCount"": 1,
  ""Dependencies"": [],
  ""SupportedFrameworks"": [
    ""net46""
  ],
  ""Hash"": ""hash"",
  ""HashAlgorithm"": ""SHA512"",
  ""PackageFileSize"": 100000,
  ""LicenseUrl"": """",
  ""RequiresLicenseAcceptance"": false
}";
        }
    }
}
