// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web;
using System.Web.Routing;
using Moq;
using Xunit;

namespace NuGetGallery.Helpers
{
    public class ObfuscationHelperFacts
    {
        public class TheObfuscateCurrentRequestUrlFacts
        {
            private const string RelativeTestPath = "account/confirm/user1/token1";
            private RouteCollection _currentRoutes;

            public TheObfuscateCurrentRequestUrlFacts()
            {
                _currentRoutes = new RouteCollection();
                Routes.RegisterApiV2Routes(_currentRoutes);
                Routes.RegisterUIRoutes(_currentRoutes, adminPanelEnabled: true);
            }

            [Fact]
            public void WithNullContextReturnsEmptyString()
            {
                // Assert + Assert
                var result = ObfuscationHelper.ObfuscateRequestUrl(null, _currentRoutes);
                Assert.Equal("", result);
            }

            [Fact]
            public void WithNullRoutesReturnsEmptyString()
            {
                //Arrange 
                var context = GetMockedHttpContext();

                // Assert + Assert
                var result = ObfuscationHelper.ObfuscateRequestUrl(context, null);
                Assert.Equal("", result);
            }

            [Fact]
            public void ValidData()
            {
                //Arrange 
                var context = GetMockedHttpContext();

                // Assert + Assert
                var result = ObfuscationHelper.ObfuscateRequestUrl(context, _currentRoutes);
                Assert.Equal("account/confirm/user1/ObfuscatedToken", result);
            }

            private HttpContextBase GetMockedHttpContext()
            {
                var context = new Mock<HttpContextBase>();
                var request = new Mock<HttpRequestBase>();
                context.Setup(ctx => ctx.Request).Returns(request.Object);
                
                request.Setup(req => req.Url).Returns(new Uri($"https://localhost/{RelativeTestPath}"));
                request.Setup(req => req.AppRelativeCurrentExecutionFilePath).Returns($"~/{RelativeTestPath}");
                return context.Object;
            }
        }
    }
}

