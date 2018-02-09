﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Specialized;
using System.Linq;
using System.Web;
using Moq;
using NuGet.Versioning;
using Xunit;

namespace NuGetGallery.Security
{
    /// <summary>
    /// This code should be removed soon: https://github.com/NuGet/Engineering/issues/800
    /// </summary>
    public class RequireMinClientVersionForPushPolicyFacts
    {
        [Theory]
        [InlineData("4.1.0")]
        [InlineData("3.0.0")]
        [InlineData("2.0.0,4.1.0")]
        [InlineData("4.1.0-beta1")]
        public void Evaluate_ReturnsSuccessIfClientVersionEqualOrHigherThanRequired(string minClientVersions)
        {
            // Arrange & Act
            var result = Evaluate(minClientVersions, actualClientVersion: "4.1.0", actualProtocolVersion: string.Empty);

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);
        }

        [Theory]
        [InlineData("4.1.0")]
        [InlineData("3.0.0")]
        [InlineData("2.0.0,4.1.0")]
        [InlineData("2.5.0")]
        public void Evaluate_ReturnsFailureIfClientVersionLowerThanRequired(string minClientVersions)
        {
            // Arrange & Act
            var result = Evaluate(minClientVersions, actualClientVersion: "2.5.0-beta1", actualProtocolVersion: string.Empty);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }

        [Theory]
        [InlineData("4.1.0")]
        [InlineData("3.0.0")]
        [InlineData("2.0.0,4.1.0")]
        [InlineData("4.1.0-beta1")]
        public void Evaluate_ReturnsSuccessIfProtocolVersionEqualOrHigherThanRequired(string minClientVersions)
        {
            // Arrange & Act
            var result = Evaluate(minClientVersions, actualClientVersion: string.Empty, actualProtocolVersion: "4.1.0");

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);
        }

        [Theory]
        [InlineData("4.1.0")]
        [InlineData("3.0.0")]
        [InlineData("2.0.0,4.1.0")]
        [InlineData("2.5.0")]
        public void Evaluate_ReturnsFailureIfProtocolVersionLowerThanRequired(string minClientVersions)
        {
            // Arrange & Act
            var result = Evaluate(minClientVersions, actualClientVersion: string.Empty, actualProtocolVersion: "2.5.0-beta1");

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public void Evaluate_ReturnsFailureIfProtocolVersionLowerThenRequiredAndClientVersionHigher()
        {
            // Arrange & Act
            var result = Evaluate("4.1.0", actualClientVersion: "4.1.0", actualProtocolVersion: "2.5.0-beta1");

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public void Evaluate_ReturnsSuccessIfProtocolVersionHigherThenRequiredAndClientVersionLower()
        {
            // Arrange & Act
            var result = Evaluate("4.1.0", actualClientVersion: "2.5.0-beta1", actualProtocolVersion: "4.1.0");

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public void Evaluate_ReturnsFailureIfClientVersionHeaderIsMissing()
        {
            // Arrange & Act
            var result = Evaluate(minClientVersions: "4.1.0", actualClientVersion: string.Empty, actualProtocolVersion: string.Empty);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }

        private SecurityPolicyResult Evaluate(string minClientVersions, string actualClientVersion, string actualProtocolVersion)
        {
            var headers = new NameValueCollection();
            if (!string.IsNullOrEmpty(actualClientVersion))
            {
                headers[Constants.ClientVersionHeaderName] = actualClientVersion;
            }

            if (!string.IsNullOrEmpty(actualProtocolVersion))
            {
                headers[Constants.NuGetProtocolHeaderName] = actualProtocolVersion;
            }

            var httpRequest = new Mock<HttpRequestBase>();
            httpRequest.Setup(r => r.Headers).Returns(headers);

            var httpContext = new Mock<HttpContextBase>();
            httpContext.Setup(c => c.Request).Returns(httpRequest.Object);

            var policies = minClientVersions.Split(',').Select(
                v => RequireMinClientVersionForPushPolicy.CreatePolicy("Subscription", new NuGetVersion(v))
            ).ToArray();
            var context = new UserSecurityPolicyEvaluationContext(policies, httpContext.Object);

            return new RequireMinClientVersionForPushPolicy().Evaluate(context);
        }
    }
}
