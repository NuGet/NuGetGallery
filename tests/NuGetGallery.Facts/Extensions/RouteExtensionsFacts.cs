﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web.Routing;
using Xunit;

namespace NuGetGallery.Extensions
{
    public class RouteExtensionsFacts
    {
        private static string _routeUrl = "test/{user}";
        private static string _url = "test/user1";
        private static int _segment = 1;
        private static string _obfuscatedValue = "obfuscatedData";

        [Fact]
        public void MapRoute_WithoutConstraints_AddsObfuscation()
        {
            // Arrange
            var routes = new RouteCollection();
            routes.MapRoute(
                "test",
                _routeUrl,
                defaults: null,
                obfuscationMetadata: new RouteExtensions.ObfuscatedMetadata(_segment, _obfuscatedValue));

            // Act + Assert
            Assert.True(RouteExtensions.ObfuscatedRouteMap.ContainsKey(_routeUrl));
            Assert.Equal(_segment, RouteExtensions.ObfuscatedRouteMap[_routeUrl][0].ObfuscatedSegment);
            Assert.Equal(_obfuscatedValue, RouteExtensions.ObfuscatedRouteMap[_routeUrl][0].ObfuscateValue);
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
                obfuscationMetadata: new RouteExtensions.ObfuscatedMetadata(_segment, _obfuscatedValue));

            // Act + Assert
            Assert.True(RouteExtensions.ObfuscatedRouteMap.ContainsKey(_routeUrl));
            Assert.Equal(_segment, RouteExtensions.ObfuscatedRouteMap[_routeUrl][0].ObfuscatedSegment);
            Assert.Equal(_obfuscatedValue, RouteExtensions.ObfuscatedRouteMap[_routeUrl][0].ObfuscateValue);
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
            routes.MapRoute("test", _routeUrl, null, new RouteExtensions.ObfuscatedMetadata(_segment, _obfuscatedValue));
            var route = new Route(url: _routeUrl, routeHandler: null);

            // Act
            var obfuscated = route.ObfuscateUrlPath(_url);

            //Assert
            Assert.Equal($"test/{_obfuscatedValue}", obfuscated);
        }
    }
}