/// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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

        public class TheExpireAnalyticsCookiesMethod
        {
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
                var rootDomain = "domain.test";
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
                Assert.Equal(rootDomain, _gaCookie.Domain);
                var _gidCookie = httpContext.Response.Cookies["_gid"];
                Assert.Equal(rootDomain, _gidCookie.Domain);
                var _gatCookie = httpContext.Response.Cookies["_gat"];
                Assert.Equal(rootDomain, _gatCookie.Domain);
                Assert.Null(httpContext.Response.Cookies["ai_user"].Domain);
                Assert.Null(httpContext.Response.Cookies["ai_session"].Domain);
            }
        }

        public class TheExpireCookieByNameMethod
        {
            [Fact]
            public void ExpireCookieByName_ThrowsIfHttpContextNull()
            {
                // Arrange
                var cookieExpirationService = new CookieExpirationService("AnyDomain");

                // Act & Assert
                var exception = Assert.Throws<ArgumentNullException>(() => cookieExpirationService.ExpireCookieByName(httpContext: null, cookieName: It.IsAny<string>()));
                Assert.Equal("httpContext", exception.ParamName);
            }

            [Fact]
            public void ExpireCookieByName_ThrowsIfCookieNameNull()
            {
                // Arrange
                var cookieExpirationService = new CookieExpirationService("AnyDomain");

                // Act & Assert
                var exception = Assert.Throws<ArgumentException>(() => cookieExpirationService.ExpireCookieByName(httpContext: Mock.Of<HttpContextBase>(), cookieName: null));
                Assert.Equal("cookieName", exception.ParamName);
                Assert.Contains("The argument cannot be null or empty", exception.Message);
            }

            [Fact]
            public void ExpireCookieByName_ThrowsIfCookieNameEmpty()
            {
                // Arrange
                var cookieExpirationService = new CookieExpirationService("AnyDomain");

                // Act & Assert
                var exception = Assert.Throws<ArgumentException>(() => cookieExpirationService.ExpireCookieByName(httpContext: Mock.Of<HttpContextBase>(), cookieName: ""));
                Assert.Equal("cookieName", exception.ParamName);
                Assert.Contains("The argument cannot be null or empty", exception.Message);
            }

            [Fact]
            public void ExpireCookieByName_ReturnsIfRequestNull()
            {
                // Arrange
                var httpContext = new Mock<HttpContextBase>();
                var cookieExpirationService = new CookieExpirationService("AnyDomain");

                // Act & Assert
                cookieExpirationService.ExpireCookieByName(httpContext.Object, "AnyCookieName");
            }

            [Fact]
            public void ExpireCookieByName_ReturnsIfResponseNull()
            {
                // Arrange
                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(c => c.Request).Returns(Mock.Of<HttpRequestBase>());
                var cookieExpirationService = new CookieExpirationService("AnyDomain");

                // Act & Assert
                cookieExpirationService.ExpireCookieByName(httpContext.Object, "AnyCookieName");
            }

            [Fact]
            public void ExpireCookieByName_ReturnsIfRequestCookiesNull()
            {
                // Arrange
                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(c => c.Request).Returns(Mock.Of<HttpRequestBase>());
                httpContext.Setup(c => c.Response).Returns(Mock.Of<HttpResponseBase>());
                var cookieExpirationService = new CookieExpirationService("AnyDomain");

                // Act & Assert
                cookieExpirationService.ExpireCookieByName(httpContext.Object, "AnyCookieName");
            }

            [Fact]
            public void ExpireCookieByName_ReturnsIfResponseCookiesNull()
            {
                // Arrange
                var httpContext = new Mock<HttpContextBase>();

                var httpRequest = new Mock<HttpRequestBase>();
                var requestCookies = new HttpCookieCollection();
                httpRequest.Setup(r => r.Cookies).Returns(requestCookies);

                httpContext.Setup(c => c.Request).Returns(httpRequest.Object);
                httpContext.Setup(c => c.Response).Returns(Mock.Of<HttpResponseBase>());

                var cookieExpirationService = new CookieExpirationService("AnyDomain");

                // Act & Assert
                cookieExpirationService.ExpireCookieByName(httpContext.Object, "AnyCookieName");
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
        }

        private static HttpContextBase GetHttpContext(IDictionary<string, string> cookies)
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
        public void GetRootDomain(string domain, string expectedRootDomain)
        {
            // Arrange
            var cookieExpirationService = new CookieExpirationService("AnyDomain");

            // Act
            var result = cookieExpirationService.GetRootDomain(domain);

            // Assert
            Assert.Equal(expectedRootDomain, result);
        }
    }
}