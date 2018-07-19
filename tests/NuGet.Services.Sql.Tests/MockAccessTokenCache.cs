// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Moq;

namespace NuGet.Services.Sql.Tests
{
    internal class MockAccessTokenCache : AccessTokenCache
    {
        public const string DefaultCertificateData = "certificateData";

        public static readonly IAuthenticationResult DefaultAccessToken = CreateValidAccessToken("default");

        public int AcquireTokenCount { get; private set; }

        public bool ThrowOnAcquireToken { get; }

        public AccessTokenCacheValue InitialValue { get; private set; }

        public AccessTokenCacheValue[] MockValues { get; }

        public MockAccessTokenCache(
            string initialCertData = DefaultCertificateData,
            string certData = DefaultCertificateData,
            IAuthenticationResult initialValue = null,
            bool throwOnAcquireToken = false,
            params IAuthenticationResult[] mockTokens)
        {
            InitialValue = (initialValue == null)
                ? null
                : new AccessTokenCacheValue(initialCertData, initialValue);

            ThrowOnAcquireToken = throwOnAcquireToken;
            MockValues = mockTokens.Select(t => new AccessTokenCacheValue(certData, t)).ToArray();
        }

        protected override bool TryGetValue(
            AzureSqlConnectionStringBuilder connectionString,
            out AccessTokenCacheValue accessToken)
        {
            var result = InitialValue;
            if (result != null)
            {
                InitialValue = null;
                accessToken = result;
                return true;
            }

            return base.TryGetValue(connectionString, out accessToken);
        }

        protected override Task<AccessTokenCacheValue> AcquireAccessTokenAsync(
            AzureSqlConnectionStringBuilder connectionString,
            string clientCertificateData)
        {
            try
            {
                if (ThrowOnAcquireToken)
                {
                    throw new InvalidOperationException("Could not acquire token");
                }

                if (MockValues.Length == 0)
                {
                    return Task.FromResult(new AccessTokenCacheValue(clientCertificateData, DefaultAccessToken));
                }

                var tokenIndex = Math.Max(AcquireTokenCount, MockValues.Length - 1);
                return Task.FromResult(MockValues[tokenIndex]);
            }
            finally
            {
                AcquireTokenCount++;
            }
        }

        public static IAuthenticationResult CreateValidAccessToken(string value = "valid")
        {
            return CreateMockAccessToken(value, DateTimeOffset.Now + TimeSpan.FromHours(1));
        }

        public static IAuthenticationResult CreateMockAccessToken(string value, DateTimeOffset expiresOn)
        {
            var mockToken = new Mock<IAuthenticationResult>();
            mockToken.Setup(x => x.AccessToken).Returns(value);
            mockToken.Setup(x => x.ExpiresOn).Returns(expiresOn);
            return mockToken.Object;
        }
    }
}
