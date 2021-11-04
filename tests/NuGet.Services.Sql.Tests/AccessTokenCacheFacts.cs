// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Services.Sql.Tests
{
    public class AccessTokenCacheFacts
    {
        public class TheGetAsyncMethod
        {
            private static readonly AzureSqlConnectionStringBuilder MockConnectionString = new AzureSqlConnectionStringBuilder(MockConnectionStrings.AadSqlConnectionString);

            [Fact]
            public async Task WhenCannotAcquireToken_ThrowsInvalidOperationException()
            {
                // Arrange.
                var tokenCache = new MockAccessTokenCache(throwOnAcquireToken: true);

                // Act.
                await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                    await tokenCache.GetAsync(
                        MockConnectionString,
                        MockAccessTokenCache.DefaultCertificateData);
                });

                // Assert.
                Assert.Equal(1, tokenCache.AcquireTokenCount);
            }

            [Fact]
            public async Task WhenNoTokenInCache_AcquiresNew()
            {
                // Arrange.
                var token1 = MockAccessTokenCache.CreateValidAccessToken();
                var tokenCache = new MockAccessTokenCache(mockTokens: token1);

                // Act.
                var result = await tokenCache.GetAsync(
                    MockConnectionString,
                    MockAccessTokenCache.DefaultCertificateData);

                // Assert.
                Assert.Equal(1, tokenCache.AcquireTokenCount);
                Assert.Equal("valid", result.AccessToken);
            }

            [Fact]
            public async Task WhenExpiredTokenInCache_AcquiresNew()
            {
                // Arrange.
                var token0 = MockAccessTokenCache.CreateMockAccessToken("expired", DateTimeOffset.Now);
                var token1 = MockAccessTokenCache.CreateValidAccessToken();
                var tokenCache = new MockAccessTokenCache(initialValue: token0, mockTokens: token1);

                // Act.
                var result = await tokenCache.GetAsync(
                    MockConnectionString,
                    MockAccessTokenCache.DefaultCertificateData);

                // Assert.
                Assert.Equal(1, tokenCache.AcquireTokenCount);
                Assert.Equal("valid", result.AccessToken);
            }

            [Fact]
            public async Task WhenNearExpiredTokenInCache_AcquiresNewInBackground()
            {
                // Arrange.
                var token0 = MockAccessTokenCache.CreateMockAccessToken("nearExpired", DateTimeOffset.Now + TimeSpan.FromMinutes(10));
                var token1 = MockAccessTokenCache.CreateValidAccessToken();
                var tokenCache = new MockAccessTokenCache(initialValue: token0, mockTokens: token1);

                // Act.
                var result = await tokenCache.GetAsync(
                    MockConnectionString,
                    MockAccessTokenCache.DefaultCertificateData);

                // Assert.
                Assert.Equal("nearExpired", result.AccessToken);

                await Task.Delay(500);
                Assert.Equal(1, tokenCache.AcquireTokenCount);
            }

            [Fact]
            public async Task WhenCertificateDataChanges_AcquiresNewInBackground()
            {
                // Arrange.
                var certData0 = "initialCertificateData";
                var token0 = MockAccessTokenCache.CreateValidAccessToken("valid0");
                var token1 = MockAccessTokenCache.CreateMockAccessToken("valid1", DateTimeOffset.Now + TimeSpan.FromMinutes(120));
                var tokenCache = new MockAccessTokenCache(initialCertData: certData0, initialValue: token0, mockTokens: token1);

                // Act.
                var result = await tokenCache.GetAsync(
                    MockConnectionString,
                    MockAccessTokenCache.DefaultCertificateData);

                // Assert.
                Assert.Equal("valid0", result.AccessToken);

                await Task.Delay(500);
                Assert.Equal(1, tokenCache.AcquireTokenCount);
            }

            [Fact]
            public async Task WhenValidTokenInCache_ReturnsExisting()
            {
                // Arrange.
                var token0 = MockAccessTokenCache.CreateValidAccessToken();
                var token1 = token0;
                var tokenCache = new MockAccessTokenCache(initialValue: token0, mockTokens: token1);

                // Act.
                var result = await tokenCache.GetAsync(
                    MockConnectionString,
                    MockAccessTokenCache.DefaultCertificateData);

                // Assert.
                Assert.Equal(0, tokenCache.AcquireTokenCount);
                Assert.Equal("valid", result.AccessToken);
            }
        }

        public class TheTryGetCachedMethod
        {
            private static readonly AzureSqlConnectionStringBuilder MockConnectionString = new AzureSqlConnectionStringBuilder(MockConnectionStrings.AadSqlConnectionString);

            [Fact]
            public void WhenNoTokenInCache_ReturnsNull()
            {
                // Arrange.
                var token1 = MockAccessTokenCache.CreateValidAccessToken();
                var tokenCache = new MockAccessTokenCache(mockTokens: token1);

                // Act.
                var success = tokenCache.TryGetCached(
                    MockConnectionString,
                    MockAccessTokenCache.DefaultCertificateData,
                    out var result);

                // Assert.
                Assert.False(success);
                Assert.Equal(0, tokenCache.AcquireTokenCount);
                Assert.Null(result);
            }

            [Fact]
            public void WhenExpiredTokenInCache_ReturnsNull()
            {
                // Arrange.
                var token0 = MockAccessTokenCache.CreateMockAccessToken("expired", DateTimeOffset.Now);
                var token1 = MockAccessTokenCache.CreateValidAccessToken();
                var tokenCache = new MockAccessTokenCache(initialValue: token0, mockTokens: token1);

                // Act.
                var success = tokenCache.TryGetCached(
                    MockConnectionString,
                    MockAccessTokenCache.DefaultCertificateData,
                    out var result);

                // Assert.
                Assert.False(success);
                Assert.Equal(0, tokenCache.AcquireTokenCount);
                Assert.Null(result);
            }

            [Fact]
            public async Task WhenNearExpiredTokenInCache_AcquiresNewInBackground()
            {
                // Arrange.
                var token0 = MockAccessTokenCache.CreateMockAccessToken("nearExpired", DateTimeOffset.Now + TimeSpan.FromMinutes(10));
                var token1 = MockAccessTokenCache.CreateValidAccessToken();
                var tokenCache = new MockAccessTokenCache(initialValue: token0, mockTokens: token1);

                // Act.
                var success = tokenCache.TryGetCached(
                    MockConnectionString,
                    MockAccessTokenCache.DefaultCertificateData,
                    out var result);

                // Assert.
                Assert.True(success);
                Assert.Equal("nearExpired", result.AccessToken);

                await Task.Delay(500);
                Assert.Equal(1, tokenCache.AcquireTokenCount);
            }

            [Fact]
            public async Task WhenCertificateDataChanges_AcquiresNewInBackground()
            {
                // Arrange.
                var certData0 = "initialCertificateData";
                var token0 = MockAccessTokenCache.CreateValidAccessToken("valid0");
                var token1 = MockAccessTokenCache.CreateMockAccessToken("valid1", DateTimeOffset.Now + TimeSpan.FromMinutes(120));
                var tokenCache = new MockAccessTokenCache(initialCertData: certData0, initialValue: token0, mockTokens: token1);

                // Act.
                var success = tokenCache.TryGetCached(
                    MockConnectionString,
                    MockAccessTokenCache.DefaultCertificateData,
                    out var result);

                // Assert.
                Assert.True(success);
                Assert.Equal("valid0", result.AccessToken);

                await Task.Delay(500);
                Assert.Equal(1, tokenCache.AcquireTokenCount);
            }

            [Fact]
            public void WhenValidTokenInCache_ReturnsExisting()
            {
                // Arrange.
                var token0 = MockAccessTokenCache.CreateValidAccessToken();
                var token1 = token0;
                var tokenCache = new MockAccessTokenCache(initialValue: token0, mockTokens: token1);

                // Act.
                var success = tokenCache.TryGetCached(
                    MockConnectionString,
                    MockAccessTokenCache.DefaultCertificateData,
                    out var result);

                // Assert.
                Assert.True(success);
                Assert.Equal(0, tokenCache.AcquireTokenCount);
                Assert.Equal("valid", result.AccessToken);
            }
        }
    }
}
