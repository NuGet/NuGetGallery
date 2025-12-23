// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace NuGetGallery.Infrastructure
{
    public class CookieTempDataProviderFacts
    {
        public class TheLoadTempDataMethod
        {
            [Fact]
            public void RetrievesValuesFromCookie()
            {
                var cookies = new HttpCookieCollection();
                var tempData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    { "message", "Say hello to my little friend" },
                    { "question", "How am I funny?" }
                };

                // Serialize and protect the tempData dictionary
                var json = JsonConvert.SerializeObject(tempData);
                var protectedBytes = MachineKey.Protect(Encoding.UTF8.GetBytes(json), "__Controller::TempData");
                var cookie = new HttpCookie("__Controller::TempData")
                {
                    HttpOnly = true,
                    Secure = true,
                    Value = Convert.ToBase64String(protectedBytes)
                };
                cookies.Add(cookie);

                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(c => c.Request.Cookies).Returns(cookies);
                ITempDataProvider provider = new CookieTempDataProvider(httpContext.Object);
                var controllerContext = new ControllerContext();

                var loadedTempData = provider.LoadTempData(controllerContext);

                Assert.Equal(2, loadedTempData.Count);
                Assert.Equal("Say hello to my little friend", loadedTempData["message"]);
                Assert.Equal("How am I funny?", loadedTempData["question"]);
                Assert.Equal("How am I funny?", loadedTempData["QUESTION"]);
            }


            [Fact]
            public void DoesNotThrowWhenKeyValuesFromCookieContainsNullKey()
            {
                var cookies = new HttpCookieCollection();
                var tempData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    { "message", "Say hello to my little friend" },
                    { "question", "How am I funny?" }
                };

                // Serialize and protect the tempData dictionary
                var json = JsonConvert.SerializeObject(tempData);
                var protectedBytes = MachineKey.Protect(Encoding.UTF8.GetBytes(json), "__Controller::TempData");
                var cookie = new HttpCookie("__Controller::TempData")
                {
                    HttpOnly = true,
                    Secure = true,
                    Value = Convert.ToBase64String(protectedBytes)
                };
                cookies.Add(cookie);

                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(c => c.Request.Cookies).Returns(cookies);
                ITempDataProvider provider = new CookieTempDataProvider(httpContext.Object);
                var controllerContext = new ControllerContext();

                var loadedTempData = provider.LoadTempData(controllerContext);

                Assert.Equal(2, loadedTempData.Count);
                Assert.Equal("Say hello to my little friend", loadedTempData["message"]);
                Assert.Equal("How am I funny?", loadedTempData["question"]);
                Assert.Equal("How am I funny?", loadedTempData["QUESTION"]);
            }

            [Fact]
            public void WithNullCookieReturnsEmptyDictionary()
            {
                var cookies = new HttpCookieCollection();
                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(c => c.Request.Cookies).Returns(cookies);
                ITempDataProvider provider = new CookieTempDataProvider(httpContext.Object);
                var controllerContext = new ControllerContext();

                var tempData = provider.LoadTempData(controllerContext);

                Assert.Empty(tempData);
            }

            [Fact]
            public void WithEmptyCookieReturnsEmptyDictionary()
            {
                var cookies = new HttpCookieCollection();
                var cookie = new HttpCookie("__Controller::TempData");
                cookie.HttpOnly = true;
                cookies.Add(cookie);
                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(c => c.Request.Cookies).Returns(cookies);
                ITempDataProvider provider = new CookieTempDataProvider(httpContext.Object);
                var controllerContext = new ControllerContext();

                var tempData = provider.LoadTempData(controllerContext);

                Assert.Empty(tempData);
            }
        }

        public class TheSaveTempDataMethod
        {
            [Fact]
            public void StoresValuesInCookieInEncodedFormat()
            {
                var cookies = new HttpCookieCollection();
                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(c => c.Response.Cookies).Returns(cookies);
                ITempDataProvider provider = new CookieTempDataProvider(httpContext.Object);
                var controllerContext = new ControllerContext();

                var tempData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    { "message", "Say hello to my little friend" },
                    { "key2", 123 },
                    { "key3", "dumb&dumber?:;,isit" }
                };
                provider.SaveTempData(controllerContext, tempData);

                Assert.Single(cookies);
                var cookie = cookies["__Controller::TempData"];
                Assert.True(cookie.HttpOnly);
                Assert.True(cookie.Secure);
                Assert.Equal(SameSiteMode.Lax, cookie.SameSite);

                // Decrypt and deserialize the cookie value
                var unprotectedBytes = MachineKey.Unprotect(Convert.FromBase64String(cookie.Value), "__Controller::TempData");
                var json = Encoding.UTF8.GetString(unprotectedBytes);
                var deserialized = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

                Assert.Equal(3, deserialized.Count);
                Assert.Equal("Say hello to my little friend", deserialized["message"]);
                Assert.Equal(123, Convert.ToInt32(deserialized["key2"]));
                Assert.Equal("dumb&dumber?:;,isit", deserialized["key3"]);
            }

            [Fact]
            public void WithNoValuesDoesNotAddCookie()
            {
                var cookies = new HttpCookieCollection();
                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(c => c.Response.Cookies).Returns(cookies);
                ITempDataProvider provider = new CookieTempDataProvider(httpContext.Object);
                var controllerContext = new ControllerContext();

                provider.SaveTempData(controllerContext, new Dictionary<string, object>());

                Assert.Empty(cookies);
            }

            [Fact]
            public void WithInitialStateAndNoValuesClearsCookie()
            {
                // Arrange and Setup
                var cookies = new HttpCookieCollection();
                var cookie = new HttpCookie("__Controller::TempData");
                cookie.HttpOnly = true;
                cookie.Secure = true;
                cookies.Add(cookie);
                cookie["message"] = "clear";
                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(c => c.Request.Cookies).Returns(cookies);
                httpContext.Setup(c => c.Response.Cookies).Returns(cookies);
                ITempDataProvider provider = new CookieTempDataProvider(httpContext.Object);
                var controllerContext = new ControllerContext();

                var tempData = provider.LoadTempData(controllerContext);

                // Validate
                provider.SaveTempData(controllerContext, new Dictionary<string, object>());
                Assert.Single(cookies);
                Assert.True(cookies[0].HttpOnly);
                Assert.True(cookies[0].Secure);
                Assert.Equal("", cookies[0].Value);
            }
        }
    }
}
