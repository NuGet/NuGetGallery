//-----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------

using System;
using System.Web;
using System.Collections.Generic;
using Moq;
using Xunit;

namespace NuGetGallery.Cookies
{
    public class CookieExpirationServiceFacts
    {
        [Fact]
        public void CreateCookieExpirationService_ThrowsIfDomainNull()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new CookieExpirationService(domain: null));
            Assert.Equal("domain", exception.ParamName);
        }

        [Fact]
        public void ExpireAnalyticsCookies_ThrowsIfHttpContextNull()
        {
            // Arrange
            var cookieExpirationService = new CookieExpirationService("AnyDomain");

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => cookieExpirationService.ExpireAnalyticsCookies(httpContext: null));
            Assert.Equal("httpContext", exception.ParamName);
        }

        [Fact]
        public void ExpireAnalyticsCookies()
        {
            // Arrange
            var cookies = new Dictionary<string, string>
            {
                { "_ga", "ga_value" },
                { "_gid", "gid_value" },
                { "_gat", "gat_value" },
                { "ai_user", "ai_user_value" },
                { "ai_session", "ai_session_value" },
            };

            var httpContext = GetHttpContext(cookies);
            var domain = "subdomain.domain.test";
            var primaryDomain = "domain.test";
            var cookieExpirationService = new CookieExpirationService(domain);

            // Act
            cookieExpirationService.ExpireAnalyticsCookies(httpContext);

            // Assert
            foreach (var key in cookies.Keys)
            {
                var responseCookie = httpContext.Response.Cookies[key];
                Assert.NotNull(responseCookie);
                Assert.True(DateTime.Equals(new DateTime(2010, 1, 1), responseCookie.Expires));
                Assert.Equal(cookies[key], responseCookie.Value);
            }

            var _gaCookie = httpContext.Response.Cookies["_ga"];
            Assert.Equal(primaryDomain, _gaCookie.Domain);
            var _gidCookie = httpContext.Response.Cookies["_gid"];
            Assert.Equal(primaryDomain, _gidCookie.Domain);
            var _gatCookie = httpContext.Response.Cookies["_gat"];
            Assert.Equal(primaryDomain, _gatCookie.Domain);
            Assert.Null(httpContext.Response.Cookies["ai_user"].Domain);
            Assert.Null(httpContext.Response.Cookies["ai_session"].Domain);
            Assert.Null(httpContext.Response.Cookies["nugetab"].Domain);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("AnyDomain")]
        public void ExpireCookieByName(string domain)
        {
            // Arrange
            var cookieName = "AnyCookieName";
            var cookieValue = "AnyCookieValue";
            var cookies = new Dictionary<string, string>
            {
                { cookieName, cookieValue}
            };
            var httpContext = GetHttpContext(cookies);
            var cookieExpirationService = new CookieExpirationService("AnyDomain");

            // Act
            cookieExpirationService.ExpireCookieByName(httpContext, cookieName, domain);

            // Assert
            var responseCookie = httpContext.Response.Cookies[cookieName];
            Assert.NotNull(responseCookie);
            Assert.True(DateTime.Equals(new DateTime(2010, 1, 1), responseCookie.Expires));
            Assert.Equal(domain, responseCookie.Domain);
            Assert.Equal(cookieValue, responseCookie.Value);
        }

        private HttpContextBase GetHttpContext(IDictionary<string, string> cookies)
        {
            var httpRequest = new Mock<HttpRequestBase>();
            var requestCookies = new HttpCookieCollection();

            var httpResponse = new Mock<HttpResponseBase>();
            var responseCookies = new HttpCookieCollection();

            foreach (var key in cookies.Keys)
            {
                var requestCookie = new HttpCookie(key, cookies[key]);
                requestCookies.Add(requestCookie);

                var responseCookie = new HttpCookie(key, cookies[key]);
                responseCookies.Add(responseCookie);
            }

            httpRequest.Setup(r => r.Cookies).Returns(requestCookies);
            httpResponse.Setup(r => r.Cookies).Returns(responseCookies);

            var httpContext = new Mock<HttpContextBase>();
            httpContext.Setup(c => c.Request).Returns(httpRequest.Object);
            httpContext.Setup(c => c.Response).Returns(httpResponse.Object);

            return httpContext.Object;
        }

        [Theory]
        [InlineData("test", "test")]
        [InlineData("domain.test", "domain.test")]
        [InlineData("subdomain.domain.test", "domain.test")]
        [InlineData("subdomain.subdomain.domain.test", "domain.test")]
        public void GetPrimaryDomain(string domain, string expectedPrimaryDomain)
        {
            // Arrange
            var cookieExpirationService = new CookieExpirationService("AnyDomain");

            // Act
            var result = cookieExpirationService.GetPrimaryDomain(domain);

            // Assert
            Assert.Equal(expectedPrimaryDomain, result);
        }
    }
}
