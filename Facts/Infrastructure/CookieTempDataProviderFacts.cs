using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;
using Moq;
using Xunit;

namespace NuGetGallery.Infrastructure {
    public class CookieTempDataProviderFacts {
        public class TheSaveTempDataMethod {
            [Fact]
            public void StoresValuesInCookie() {
                var cookies = new HttpCookieCollection();
                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(c => c.Response.Cookies).Returns(cookies);
                ITempDataProvider provider = new CookieTempDataProvider(httpContext.Object);
                var controllerContext = new ControllerContext();

                provider.SaveTempData(controllerContext, new Dictionary<string, object> { 
                    { "message", "Say hello to my little friend" },
                    { "key2", 123 } 
                });

                Assert.Equal(1, cookies.Count);
                Assert.True(cookies[0].HttpOnly);
                Assert.Equal(2, cookies[0].Values.Count);
                Assert.Equal("Say hello to my little friend", cookies[0]["message"]);
                Assert.Equal("123", cookies[0]["key2"]);
            }

            [Fact]
            public void WithNoValuesDoesNotAddCookie() {
                var cookies = new HttpCookieCollection();
                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(c => c.Response.Cookies).Returns(cookies);
                ITempDataProvider provider = new CookieTempDataProvider(httpContext.Object);
                var controllerContext = new ControllerContext();

                provider.SaveTempData(controllerContext, new Dictionary<string, object>());

                Assert.Equal(0, cookies.Count);
            }
        }

        public class TheLoadTempDataMethod {
            [Fact]
            public void RetrievesValuesFromCookie() {
                var cookies = new HttpCookieCollection();
                var cookie = new HttpCookie("__Controller::TempData");
                cookies.Add(cookie);
                cookie["message"] = "Say hello to my little friend";
                cookie["question"] = "How am I funny?";
                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(c => c.Request.Cookies).Returns(cookies);
                ITempDataProvider provider = new CookieTempDataProvider(httpContext.Object);
                var controllerContext = new ControllerContext();

                var tempData = provider.LoadTempData(controllerContext);

                Assert.Equal(2, tempData.Count);
                Assert.Equal("Say hello to my little friend", tempData["message"]);
                Assert.Equal("How am I funny?", tempData["question"]);
            }

            [Fact]
            public void WithNullCookieReturnsEmptyDictionary() {
                var cookies = new HttpCookieCollection();
                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(c => c.Request.Cookies).Returns(cookies);
                ITempDataProvider provider = new CookieTempDataProvider(httpContext.Object);
                var controllerContext = new ControllerContext();

                var tempData = provider.LoadTempData(controllerContext);

                Assert.Equal(0, tempData.Count);
            }

            [Fact]
            public void WithEmptyCookieReturnsEmptyDictionary() {
                var cookies = new HttpCookieCollection();
                var cookie = new HttpCookie("__Controller::TempData");
                cookies.Add(cookie);
                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(c => c.Request.Cookies).Returns(cookies);
                ITempDataProvider provider = new CookieTempDataProvider(httpContext.Object);
                var controllerContext = new ControllerContext();

                var tempData = provider.LoadTempData(controllerContext);

                Assert.Equal(0, tempData.Count);
            }
        }
    }
}
