using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Helpers;
using Moq;
using Xunit;

namespace NuGetGallery.Infrastructure
{
    public class AntiForgeryExtensionsFacts
    {
        public class TheGetAntiForgeryCookieMethod
        {
            [Fact]
            public void RequiresNonNullRequest()
            {
                var ex = Assert.Throws<ArgumentNullException>(() => AntiForgeryExtensions.GetAntiForgeryCookie(null));
                Assert.Equal("self", ex.ParamName);
            }

            [Fact]
            public void ReturnsNullIfNoCookiePresent()
            {
                var req = CreateMockRequest();
                Assert.Null(req.GetAntiForgeryCookie());
            }

            [Fact]
            public void ReturnsCookieValueIfPresent()
            {
                var req = CreateMockRequest(AntiForgeryConfig.CookieName, "abc123");
                Assert.Equal("abc123", req.GetAntiForgeryCookie());
            }
        }

        public class TheSetAntiForgeryCookieMethod
        {
            [Fact]
            public void RequiresNonNullResponse()
            {
                var ex = Assert.Throws<ArgumentNullException>(() => AntiForgeryExtensions.SetAntiForgeryCookie(null, "blah"));
                Assert.Equal("self", ex.ParamName);
            }

            [Fact]
            public void RequiresNonNullCookie()
            {
                var resp = CreateMockResponse();
                var ex = Assert.Throws<ArgumentException>(() => AntiForgeryExtensions.SetAntiForgeryCookie(resp, null));
                Assert.Equal("'value' must be a non-empty string\r\nParameter name: value", ex.Message);
                Assert.Equal("value", ex.ParamName);
            }

            [Fact]
            public void StoresProvidedValueInAppropriateCookie()
            {
                var resp = CreateMockResponse();
                resp.SetAntiForgeryCookie("abc123");

                HttpCookie afCookie = resp.Cookies[AntiForgeryConfig.CookieName];
                Assert.NotNull(afCookie);
                Assert.Equal("abc123", afCookie.Value);
                Assert.True(afCookie.HttpOnly);
            }

            [Fact]
            public void ReplacesExistingCookie()
            {
                var resp = CreateMockResponse();
                resp.Cookies.Add(new HttpCookie(AntiForgeryConfig.CookieName, "def456"));
                resp.SetAntiForgeryCookie("abc123");

                HttpCookie afCookie = resp.Cookies[AntiForgeryConfig.CookieName];
                Assert.NotNull(afCookie);
                Assert.Equal("abc123", afCookie.Value);
                Assert.True(afCookie.HttpOnly);
            }
        }

        private static HttpResponseBase CreateMockResponse()
        {
            var mock = new Mock<HttpResponseBase>();
            var mockCookies = new HttpCookieCollection();
            mock.Setup(r => r.Cookies).Returns(mockCookies);
            return mock.Object;
        }

        private static HttpRequestBase CreateMockRequest()
        {
            var mock = new Mock<HttpRequestBase>();
            var mockCookies = new HttpCookieCollection();
            mock.Setup(r => r.Cookies).Returns(mockCookies);
            return mock.Object;
        }

        private static HttpRequestBase CreateMockRequest(string cookieName, string cookieValue)
        {
            var mock = CreateMockRequest();
            mock.Cookies.Add(new HttpCookie(cookieName, cookieValue));
            return mock;
        }
    }
}
