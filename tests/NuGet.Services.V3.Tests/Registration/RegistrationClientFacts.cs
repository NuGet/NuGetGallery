// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Moq;
using NuGet.Protocol.Catalog;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Protocol.Registration
{
    public class RegistrationClientFacts
    {
        public class GetIndexAsync : BaseFacts
        {
            public GetIndexAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task ReturnsNullForNotFound()
            {
                _simpleHttpClient
                    .Setup(x => x.DeserializeUrlAsync<RegistrationIndex>(It.IsAny<string>()))
                    .Returns<string>(u => Task.FromResult(new ResponseAndResult<RegistrationIndex>(
                        HttpMethod.Get,
                        u,
                        HttpStatusCode.NotFound,
                        "Not Found",
                        hasResult: false,
                        result: null)));

                var result = await _target.GetIndexOrNullAsync(_fakeUrl);

                Assert.Null(result);
            }

            [Fact]
            public async Task WorksWithInlinedNuGetOrgSnapshot()
            {
                // Arrange
                using (var httpClient = new HttpClient(new TestDataHttpMessageHandler()))
                {
                    var client = GetClient(httpClient);

                    // Act
                    var actual = await client.GetIndexOrNullAsync(TestData.RegistrationIndexInlinedItemsUrl);

                    // Assert
                    Assert.NotNull(actual);
                    Assert.Equal(1, actual.Count);
                    Assert.Equal(4, actual.Items.First().Count);
                }
            }

            [Fact]
            public async Task WorksWithNonInlinedNuGetOrgSnapshot()
            {
                // Arrange
                using (var httpClient = new HttpClient(new TestDataHttpMessageHandler()))
                {
                    var client = GetClient(httpClient);

                    // Act
                    var actual = await client.GetIndexOrNullAsync(TestData.RegistrationIndexWithoutInlinedItemsUrl);

                    // Assert
                    Assert.NotNull(actual);
                    Assert.Equal(19, actual.Count);
                    Assert.Equal(64, actual.Items.First().Count);
                    Assert.Equal(55, actual.Items.Last().Count);
                }
            }
        }

        public class GetPageAsync : BaseFacts
        {
            public GetPageAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task ThrowsForNotFound()
            {
                _simpleHttpClient
                    .Setup(x => x.DeserializeUrlAsync<RegistrationPage>(It.IsAny<string>()))
                    .Returns<string>(u => Task.FromResult(new ResponseAndResult<RegistrationPage>(
                        HttpMethod.Get,
                        u,
                        HttpStatusCode.NotFound,
                        "Not Found",
                        hasResult: false,
                        result: null)));

                await Assert.ThrowsAsync<SimpleHttpClientException>(
                    () => _target.GetPageAsync(_fakeUrl));
            }

            [Fact]
            public async Task WorksWithNuGetOrgSnapshot()
            {
                // Arrange
                using (var httpClient = new HttpClient(new TestDataHttpMessageHandler()))
                {
                    var client = GetClient(httpClient);

                    // Act
                    var actual = await client.GetPageAsync(TestData.RegistrationPageUrl);

                    // Assert
                    Assert.NotNull(actual);
                    Assert.Equal(55, actual.Count);
                }
            }
        }

        public class GetLeafAsync : BaseFacts
        {
            public GetLeafAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task ThrowsForNotFound()
            {
                _simpleHttpClient
                    .Setup(x => x.DeserializeUrlAsync<RegistrationLeaf>(It.IsAny<string>()))
                    .Returns<string>(u => Task.FromResult(new ResponseAndResult<RegistrationLeaf>(
                        HttpMethod.Get,
                        u,
                        HttpStatusCode.NotFound,
                        "Not Found",
                        hasResult: false,
                        result: null)));

                await Assert.ThrowsAsync<SimpleHttpClientException>(
                    () => _target.GetLeafAsync(_fakeUrl));
            }

            [Fact]
            public async Task WorksWithUnlistedNuGetOrgSnapshot()
            {
                // Arrange
                using (var httpClient = new HttpClient(new TestDataHttpMessageHandler()))
                {
                    var client = GetClient(httpClient);

                    // Act
                    var actual = await client.GetLeafAsync(TestData.RegistrationLeafUnlistedUrl);

                    // Assert
                    Assert.NotNull(actual);
                    Assert.False(actual.Listed);
                    Assert.Equal(
                        "https://api.nuget.org/v3/catalog0/data/2018.11.13.04.43.04/microbuild.core.0.1.1.json",
                        actual.CatalogEntry);
                }
            }

            [Fact]
            public async Task WorksWithListedNuGetOrgSnapshot()
            {
                // Arrange
                using (var httpClient = new HttpClient(new TestDataHttpMessageHandler()))
                {
                    var client = GetClient(httpClient);

                    // Act
                    var actual = await client.GetLeafAsync(TestData.RegistrationLeafListedUrl);

                    // Assert
                    Assert.NotNull(actual);
                    Assert.True(actual.Listed);
                    Assert.Equal(
                        "https://api.nuget.org/v3/catalog0/data/2018.11.27.18.15.55/newtonsoft.json.12.0.1.json",
                        actual.CatalogEntry);
                }
            }
        }

        public abstract class BaseFacts
        {
            protected readonly ITestOutputHelper _output;
            protected readonly Mock<ISimpleHttpClient> _simpleHttpClient;
            protected readonly string _fakeUrl;
            protected readonly RegistrationClient _target;

            public BaseFacts(ITestOutputHelper output)
            {
                _output = output;
                _simpleHttpClient = new Mock<ISimpleHttpClient>();

                _fakeUrl = "https://example/nuget.versioning/something.json";

                _target = new RegistrationClient(_simpleHttpClient.Object);
            }

            protected RegistrationClient GetClient(HttpClient httpClient)
            {
                return new RegistrationClient(new SimpleHttpClient(
                    httpClient,
                    _output.GetLogger<SimpleHttpClient>()));
            }
        }
    }
}
