// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Web.Routing;
using Xunit;

namespace NuGetGallery.Extensions
{
    public class RouteExtensionsFacts
    {
        private const string _routeUrl = "test/{user}";
        private const string _url = "test/user1";
        private const int _segment = 1;
        private const string _obfuscatedValue = "obfuscatedData";

        [Fact]
        public void MapRoute_WithoutConstraints_AddsObfuscation()
        {
            // Arrange
            var routes = new RouteCollection();
            routes.MapRoute(
                "test",
                _routeUrl,
                defaults: null,
                obfuscationMetadata: new RouteExtensions.ObfuscatedPathMetadata(_segment, _obfuscatedValue));

            // Act + Assert
            Assert.True(RouteExtensions.ObfuscatedRouteMap.ContainsKey(_routeUrl));
            Assert.Equal(_segment, RouteExtensions.ObfuscatedRouteMap[_routeUrl][0].ObfuscatedSegment);
            Assert.Equal(_obfuscatedValue, RouteExtensions.ObfuscatedRouteMap[_routeUrl][0].ObfuscatedSegmentValue);
        }

        [Fact]
        public void MapRoute_WithConstraints_AddsObfuscation()
        {
            // Arrange
            var routes = new RouteCollection();
            routes.MapRoute(
                "test",
                _routeUrl,
                defaults: null,
                constraints: null,
                obfuscationMetadata: new RouteExtensions.ObfuscatedPathMetadata(_segment, _obfuscatedValue));

            // Act + Assert
            Assert.True(RouteExtensions.ObfuscatedRouteMap.ContainsKey(_routeUrl));
            Assert.Equal(_segment, RouteExtensions.ObfuscatedRouteMap[_routeUrl][0].ObfuscatedSegment);
            Assert.Equal(_obfuscatedValue, RouteExtensions.ObfuscatedRouteMap[_routeUrl][0].ObfuscatedSegmentValue);
        }

        [Fact]
        public void ObfuscateUrlPath_ReturnsNullWhenNotObfuscated()
        {
            //Arrange
            var urlInput = "newtest/{user}";
            var route = new Route(url: urlInput, routeHandler: null);

            // Act
            var obfuscated = route.ObfuscateUrlPath("newtest/user1");

            //Assert
            Assert.Null(obfuscated);
        }

        [Fact]
        public void ObfuscateUrlPath_ReturnsObfuscatedPathWhenObfuscated()
        {
            //Arrange
            var routes = new RouteCollection();
            routes.MapRoute("test", _routeUrl, null, new RouteExtensions.ObfuscatedPathMetadata(_segment, _obfuscatedValue));
            var route = new Route(url: _routeUrl, routeHandler: null);

            // Act
            var obfuscated = route.ObfuscateUrlPath(_url);

            //Assert
            Assert.Equal($"test/{_obfuscatedValue}", obfuscated);
        }

        [Fact]
        public void ObfuscateUrlQuery_ValidateDefaultObfuscationQueryParameters()
        {
            // Arange
            var parameters = RouteExtensions.ObfuscatedReturnUrlMetadata.Select(m => m.ObfuscatedQueryParameter).ToList();

           // Act + Assert
            Assert.True(parameters.Contains("returnUrl"));
            Assert.True(parameters.Contains("ReturnUrl"));
        }

        [Theory]
        [InlineData("https://localhost:550/users/account?ReturnUrl=abd&Id=1", "https://localhost:550/users/account?ReturnUrl=ObfuscatedReturnUrl&Id=1")]
        [InlineData("https://localhost:443/users/account?ReturnUrl=abd&Id=1", "https://localhost/users/account?ReturnUrl=ObfuscatedReturnUrl&Id=1")]
        [InlineData("https://localhost:550/users/account?ReturnUrl=abd", "https://localhost:550/users/account?ReturnUrl=ObfuscatedReturnUrl")]
        [InlineData("https://localhost:550/users/account?returnUrl=abd&Id=1", "https://localhost:550/users/account?returnUrl=ObfuscatedReturnUrl&Id=1")]
        [InlineData("https://localhost:443/users/account?returnUrl=abd&Id=1", "https://localhost/users/account?returnUrl=ObfuscatedReturnUrl&Id=1")]
        [InlineData("https://localhost:550/users/account?returnUrl=abd", "https://localhost:550/users/account?returnUrl=ObfuscatedReturnUrl")]
        public void ObfuscateUrlQuery_ReturnsObfuscatedUriWhenObfuscated(string input, string expected)
        {
            //Arrange
            Uri data = new Uri(input);

            // Act
            var obfuscated = RouteExtensions.ObfuscateUrlQuery(data, RouteExtensions.ObfuscatedReturnUrlMetadata);

            //Assert
            Assert.Equal(expected, obfuscated.ToString());
        }

        [Fact]
        public void ObfuscateUrlQueryDefault_ReturnsNotObfuscatedWhenNotNeeded()
        {
            //Arrange
            Uri data = new Uri("https://localhost:550/users/account?Id=1");

            // Act
            var obfuscated = RouteExtensions.ObfuscateUrlQuery(data, RouteExtensions.ObfuscatedReturnUrlMetadata);

            //Assert
            Assert.Equal(data.ToString(), obfuscated.ToString());
        }

        [Fact]
        public void ObfuscateUrlQueryDefault_ThrowWhenUriNull()
        {
            //Act + Assert
            Assert.Throws<ArgumentNullException>(()=>RouteExtensions.ObfuscateUrlQuery(null, RouteExtensions.ObfuscatedReturnUrlMetadata));
        }

        [Fact]
        public void ObfuscateUrlQueryDefault_ThrowWhenMetadataIsNull()
        {
            //Act + Assert
            Assert.Throws<ArgumentNullException>(() => RouteExtensions.ObfuscateUrlQuery(new Uri("https://localhost:550/users/account"), null));
        }

        [Fact]
        public void ObfuscateUrlQueryDefault_ReturnsNotObfuscatedWhenQueryIsEmpty()
        {
            //Arrange
            Uri data = new Uri("https://localhost:550/users/account");

            // Act
            var obfuscated = RouteExtensions.ObfuscateUrlQuery(data, RouteExtensions.ObfuscatedReturnUrlMetadata);

            //Assert
            Assert.Equal(data.ToString(), obfuscated.ToString());
        }
    }
}