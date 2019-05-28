// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Routing;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Moq;
using NuGetGallery.Services.Telemetry;
using Xunit;

namespace NuGetGallery.Telemetry
{
    public class ClientTelemetryPIIProcessorTests 
    {
        private RouteCollection _currentRoutes ;

        public ClientTelemetryPIIProcessorTests()
        {
            if (_currentRoutes == null)
            {
                _currentRoutes = new RouteCollection();
                Routes.RegisterApiV2Routes(_currentRoutes);
                Routes.RegisterUIRoutes(_currentRoutes);
            }
        }

        [Fact]
        public void NullTelemetryItemDoesNotThorw()
        {
            // Arange
            var piiProcessor = CreatePIIProcessor();

            // Act
            piiProcessor.Process(null);
        }

        [Theory]
        [MemberData(nameof(PIIUrlDataGenerator))]
        public void UrlIsUpdatedOnPIIAction(string routePath, string inputUrl, string expectedOutputUrl)
        {
            // Arange
            var piiProcessor = CreatePIIProcessor(routePath);
            RequestTelemetry telemetryItem = new RequestTelemetry();
            telemetryItem.Url = new Uri(inputUrl);
            telemetryItem.Name = $"GET {telemetryItem.Url.AbsolutePath}";

            // Act
            piiProcessor.Process(telemetryItem);

            // Assert
            Assert.Equal(expectedOutputUrl, telemetryItem.Url.ToString());
            Assert.Equal($"GET {(new Uri(expectedOutputUrl)).AbsolutePath}", $"GET {telemetryItem.Url.AbsolutePath}");
        }

        [Fact]
        public void ValidatePIIActions()
        {
            // Arange
            HashSet<string> existentPIIOperations = Obfuscator.ObfuscatedActions;
            List<string> piiOperationsFromRoutes = GetPIIOperationsFromRoute();

            // Act and Assert
            Assert.True(existentPIIOperations.SetEquals(piiOperationsFromRoutes));
        }

        [Fact]
        public void ValidatePIIRoutes()
        {
            // Arange
            List<string> piiUrlRoutes = GetPIIRoutesUrls();

            // Act and Assert
            foreach (var route in piiUrlRoutes)
            {
                var expectedTrue = RouteExtensions.ObfuscatedRouteMap.ContainsKey(route);
                Assert.True(expectedTrue, $"Route {route} was not added to the obfuscated routeMap.");
            }
        }

        private ClientTelemetryPIIProcessor CreatePIIProcessor(string url = "")
        {
            return new TestClientTelemetryPIIProcessor(new TestProcessorNext(), url );
        }

        private class TestProcessorNext : ITelemetryProcessor
        {
            public void Process(ITelemetry item)
            {
            }
        }

        private class TestClientTelemetryPIIProcessor : ClientTelemetryPIIProcessor
        {
            private string _url = string.Empty;

            public TestClientTelemetryPIIProcessor(ITelemetryProcessor next, string url) : base (next)
            {
                _url = url;
            }

            public override Route GetCurrentRoute()
            {
                var handler = new Mock<IRouteHandler>();
                return new Route(_url, handler.Object);
            }

        }

        private List<string> GetPIIOperationsFromRoute()
        {
            var piiRoutes = _currentRoutes.Where((r) =>
            {
                Route webRoute = r as Route;
                return webRoute != null ? IsPIIUrl(webRoute.Url.ToString()) : false;
            }).Select((r) => {
                var dd = ((Route)r).Defaults;
                return $"{dd["controller"]}/{dd["action"]}";
            }).Distinct().ToList();

            return piiRoutes;
        }

        private List<string> GetPIIRoutesUrls()
        {
            var piiRoutes = _currentRoutes.Where((r) =>
            {
                Route webRoute = r as Route;
                return webRoute != null ? IsPIIUrl(webRoute.Url.ToString()) : false;
            }).Select((r) => ((Route)r).Url).Distinct().ToList();

            return piiRoutes;
        }

        private static bool IsPIIUrl(string url)
        {
            return url.ToLower().Contains("username") || url.ToLower().Contains("accountname");
        }

        public static IEnumerable<object[]> PIIUrlDataGenerator()
        {
            foreach (var user in GenerateUserNames())
            {
                yield return new string[] { "packages/{id}/owners/{username}/confirm/{token}",  $"https://localhost/packages/pack1/owners/user1/confirm/sometoken", $"https://localhost/packages/pack1/owners/ObfuscatedUserName/confirm/ObfuscatedToken" };
                yield return new string[] { "packages/{id}/owners/{username}/reject/{token}", $"https://localhost/packages/pack1/owners/user1/reject/sometoken", $"https://localhost/packages/pack1/owners/ObfuscatedUserName/reject/ObfuscatedToken" };
                yield return new string[] { "packages/{id}/owners/{username}/cancel/{token}", $"https://localhost/packages/pack1/owners/user1/cancel/sometoken", $"https://localhost/packages/pack1/owners/ObfuscatedUserName/cancel/ObfuscatedToken" };

                yield return new string[] { "account/confirm/{accountName}/{token}", $"https://localhost/account/confirm/user1/sometoken", $"https://localhost/account/confirm/ObfuscatedUserName/ObfuscatedToken" };
                yield return new string[] { "account/delete/{accountName}", "https://localhost/account/delete/user1", $"https://localhost/account/delete/ObfuscatedUserName" };

                yield return new string[] { "profiles/{username}", $"https://localhost/profiles/user1", $"https://localhost/profiles/ObfuscatedUserName" };
                yield return new string[] { "account/setpassword/{username}/{token}", $"https://localhost/account/setpassword/user1/sometoken", $"https://localhost/account/setpassword/ObfuscatedUserName/ObfuscatedToken" };
                yield return new string[] { "account/forgotpassword/{username}/{token}", $"https://localhost/account/forgotpassword/user1/sometoken", $"https://localhost/account/forgotpassword/ObfuscatedUserName/ObfuscatedToken" };
            }
        }

        public static List<string> GenerateUserNames()
        {
            return new List<string>{ "user1", "user.1", "user_1", "user-1"};
        }
    }
}
