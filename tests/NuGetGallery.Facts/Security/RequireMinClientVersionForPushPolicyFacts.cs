// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Specialized;
using System.Linq;
using System.Web;
using Moq;
using Xunit;

namespace NuGetGallery.Security
{
    public class RequireMinClientVersionForPushPolicyFacts
    {
        [Theory]
        [InlineData("4.1.0")]
        [InlineData("3.0.0")]
        [InlineData("2.0.0,4.1.0")]
        [InlineData("4.1.0-beta1")]
        public void EvaluateReturnsSuccessIfClientVersionEqualOrHigher(string minClientVersions)
        {
            // Arrange & Act
            var result = Evaluate(minClientVersions, actualClientVersion: "4.1.0");

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);
        }

        [Theory]
        [InlineData("4.1.0")]
        [InlineData("3.0.0")]
        [InlineData("2.0.0,4.1.0")]
        [InlineData("2.5.0")]
        public void EvaluateReturnsFailureIfClientVersionLower(string minClientVersions)
        {
            // Arrange & Act
            var result = Evaluate(minClientVersions, actualClientVersion: "2.5.0-beta1");

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public void EvaluateReturnsFailureIfNoClientHeader()
        {
            // Arrange & Act
            var result = Evaluate(minClientVersions: "4.1.0", actualClientVersion: "");

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }

        private static UserSecurityPolicy CreateMinClientVersionForPushPolicy(string minClientVersion)
        {
            return new UserSecurityPolicy("RequireMinClientVersionForPushPolicy")
            {
                Value = $"{{\"v\":\"{minClientVersion}\"}}"
            };
        }

        private SecurityPolicyResult Evaluate(string minClientVersions, string actualClientVersion)
        {
            var headers = new NameValueCollection();
            if (!string.IsNullOrEmpty(actualClientVersion))
            {
                headers[Constants.ClientVersionHeaderName] = actualClientVersion;
            };

            var httpRequest = new Mock<HttpRequestBase>();
            httpRequest.Setup(r => r.Headers).Returns(headers);

            var httpContext = new Mock<HttpContextBase>();
            httpContext.Setup(c => c.Request).Returns(httpRequest.Object);

            var policies = minClientVersions.Split(',').Select(
                v => CreateMinClientVersionForPushPolicy(v)
            ).ToArray();
            var context = new UserSecurityPolicyContext(httpContext.Object, policies);

            return new RequireMinClientVersionForPushPolicy().Evaluate(context);
        }
    }
}
