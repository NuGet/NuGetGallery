// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Routing;
using Moq;
using Xunit;

namespace NuGetGallery.Telemetry
{
    public class QuietLogFacts
    {
        [Theory]
        [MemberData(nameof(IsPIIRouteFactsValidDataGenerator))]
        public void IsPIIRouteFacts(string controller, string action)
        {
            // Arange
            RouteData route = new RouteData();
            route.Values.Add("controller", controller);
            route.Values.Add("action", action);

            // Act
            string operation;
            bool result = QuietLog.IsPIIRoute(route, out operation);

            // Assert
            Assert.True(result);
            Assert.Equal($"{controller}/{action}", operation);
        }

        [Theory]
        [InlineData("Users", "Delete")]
        public void GetObfuscatedServerVariablesValidCase(string controller, string action)
        {
            // Arange
            RouteData route = new RouteData();
            route.Values.Add("controller", controller);
            route.Values.Add("action", action);

            var context = new TestHttpContext(route);

            // Act
            var serverVariables = QuietLog.GetObfuscatedServerVariables(context);

            // Assert
            Assert.Equal(Obfuscator.DefaultObfuscatedUrl(context.Request.Url), serverVariables["HTTP_REFERER"]);
            Assert.Equal(context.Operation, serverVariables["PATH_INFO"]);
            Assert.Equal(context.Operation, serverVariables["PATH_TRANSLATED"]);
            Assert.Equal(context.Operation, serverVariables["SCRIPT_NAME"]);
            Assert.Equal(Obfuscator.DefaultObfuscatedUrl(context.Request.Url), serverVariables["URL"]);
        }

        [Fact]
        public void GetObfuscatedServerVariablesNullRouteData()
        {
            // Arange
            var context = new TestHttpContext(null);

            // Act
            var serverVariables = QuietLog.GetObfuscatedServerVariables(context);

            // Assert
            Assert.Null(serverVariables);
        }

        [Fact]
        public void GetObfuscatedServerVariablesNullContext()
        {
            // Arrange and Act
            var serverVariables = QuietLog.GetObfuscatedServerVariables(null);

            // Assert
            Assert.Null(serverVariables);
        }

        [Fact]
        public void GetObfuscatedServerVariablesNullRequest()
        {
            // Arange
            var context = new NullRequestHttpContext();

            // Act
            var serverVariables = QuietLog.GetObfuscatedServerVariables(context);

            // Assert
            Assert.Null(serverVariables);
        }

        [Fact]
        public void GetObfuscatedServerVariablesNullRequestContext()
        {
            // Arange
            var context = new TestHttpContextNullRequestContext();

            // Act
            var serverVariables = QuietLog.GetObfuscatedServerVariables(context);

            // Assert
            Assert.Null(serverVariables);
        }


        public static IEnumerable<object[]> IsPIIRouteFactsValidDataGenerator()
        {
            return Obfuscator.ObfuscatedActions.Select(o => o.Split('/'));
        }

        private class TestHttpContext : HttpContextBase
        {
            private RouteData _routeData;

            public string Operation { get; }

            public TestHttpContext(RouteData routeData)
            {
                _routeData = routeData;
                Operation = routeData != null ? $"{routeData.Values["controller"]}/{routeData.Values["action"]}" : string.Empty;
            }
            public override HttpRequestBase Request
            {
                get
                {
                    var requestContext = new Mock<RequestContext>();
                    requestContext.Setup(m => m.RouteData).Returns(_routeData);

                    var request = new Mock<HttpRequestBase>();
                    request.Setup(m => m.RequestContext).Returns(requestContext.Object);
                    request.Setup(m => m.Url).Returns(new Uri($"https://localhost/{Operation}"));
                    return request.Object;
                }
            }
        }

        private class NullRequestHttpContext : HttpContextBase
        {
            public override HttpRequestBase Request
            {
                get
                {
                    return null;
                }
            }
        }

        private class TestHttpContextNullRequestContext : HttpContextBase
        {
            public override HttpRequestBase Request
            {
                get
                {
                    RequestContext requestContext = null;
                    var request = new Mock<HttpRequestBase>();
                    request.Setup(m => m.RequestContext).Returns(requestContext);
                    return request.Object;
                }
            }
        }

    }
}
