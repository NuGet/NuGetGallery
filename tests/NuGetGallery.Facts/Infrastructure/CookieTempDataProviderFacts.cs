﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;
using Moq;
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
                var cookie = new HttpCookie("__Controller::TempData");
                cookie.HttpOnly = true;
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
                Assert.Equal("How am I funny?", tempData["QUESTION"]);
            }


            [Fact]
            public void DoesNotThrowWhenKeyValuesFromCookieContainsNullKey()
            {
                var cookies = new HttpCookieCollection();
                var cookie = new HttpCookie("__Controller::TempData");
                cookie.HttpOnly = true;
                cookies.Add(cookie);
                cookie["message"] = "Say hello to my little friend";
                cookie["question"] = "How am I funny?";
                cookie[null] = "This should be ignored.";
                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(c => c.Request.Cookies).Returns(cookies);
                ITempDataProvider provider = new CookieTempDataProvider(httpContext.Object);
                var controllerContext = new ControllerContext();

                var tempData = provider.LoadTempData(controllerContext);

                Assert.Equal(2, tempData.Count);
                Assert.Equal("Say hello to my little friend", tempData["message"]);
                Assert.Equal("How am I funny?", tempData["question"]);
                Assert.Equal("How am I funny?", tempData["QUESTION"]);
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

                Assert.Equal(0, tempData.Count);
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

                Assert.Equal(0, tempData.Count);
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

                provider.SaveTempData(
                    controllerContext,
                    new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "message", "Say hello to my little friend" },
                            { "key2", 123 },
                            { "key3", "dumb&dumber?:;,isit" }
                        });

                Assert.Single(cookies);
                Assert.True(cookies[0].HttpOnly);
                Assert.True(cookies[0].Secure);
                Assert.Equal(3, cookies[0].Values.Count);
                Assert.Equal(HttpUtility.UrlEncode("Say hello to my little friend"), cookies[0]["message"]);
                Assert.Equal(HttpUtility.UrlEncode("123"), cookies[0]["key2"]);
                Assert.Equal(HttpUtility.UrlEncode("dumb&dumber?:;,isit"), cookies[0]["key3"]);
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