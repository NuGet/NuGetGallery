// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Principal;
using System.Web;
using System.Web.Routing;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Moq;
using Xunit;

namespace NuGetGallery.Telemetry
{
    public class ClientTelemetryPIIProcessorTests
    {
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
        public void UrlIsUpdatedOnPIIAction(string inputUrl, string expectedOutputUrl)
        {
            // Arange
            var piiProcessor = CreatePIIProcessor();
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
            string templateKey;
            foreach (var route in piiUrlRoutes)
            {
                var match = Obfuscator.NeedsObfuscation($"/{route}", out templateKey);
                Assert.True(match, $"Route {route} did not match.");
            }
        }

        private ClientTelemetryPIIProcessor CreatePIIProcessor()
        {
            return new ClientTelemetryPIIProcessor(new TestProcessorNext());
        }

        private class TestProcessorNext : ITelemetryProcessor
        {
            public void Process(ITelemetry item)
            {
            }
        }

        private static List<string> GetPIIOperationsFromRoute()
        {
            var currentRoutes = new RouteCollection();
            NuGetGallery.Routes.RegisterApiV2Routes(currentRoutes);
            NuGetGallery.Routes.RegisterUIRoutes(currentRoutes);

            var piiRoutes = currentRoutes.Where((r) =>
            {
                Route webRoute = r as Route;
                return webRoute != null ? IsPIIUrl(webRoute.Url.ToString()) : false;
            }).Select((r) => {
                var dd = ((Route)r).Defaults;
                return $"{dd["controller"]}/{dd["action"]}";
            }).Distinct().ToList();

            return piiRoutes;
        }

        private static List<string> GetPIIRoutesUrls()
        {
            var currentRoutes = new RouteCollection();
            NuGetGallery.Routes.RegisterApiV2Routes(currentRoutes);
            NuGetGallery.Routes.RegisterUIRoutes(currentRoutes);

            var piiRoutes = currentRoutes.Where((r) =>
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

        public static IEnumerable<string[]> PIIUrlDataGenerator()
        {
            foreach (var user in GenerateUserNames())
            {
                yield return new string[] { $"https://localhost/packages/pack1/owners/{user}/confirm/sometoken", $"https://localhost/packages/pack1/owners/ObfuscatedUserName/confirm/sometoken" };
                yield return new string[] { $"https://localhost/packages/pack1/owners/{user}/reject/sometoken", $"https://localhost/packages/pack1/owners/ObfuscatedUserName/reject/sometoken" };
                yield return new string[] { $"https://localhost/packages/pack1/owners/{user}/cancel/sometoken", $"https://localhost/packages/pack1/owners/ObfuscatedUserName/cancel/sometoken" };

                yield return new string[] { $"https://localhost/account/confirm/{user}/sometoken", $"https://localhost/account/confirm/ObfuscatedUserName/sometoken" };
                yield return new string[] { "https://localhost/account/delete/{user}", $"https://localhost/account/delete/ObfuscatedUserName" };

                yield return new string[] { $"https://localhost/profiles/{user}", $"https://localhost/profiles/ObfuscatedUserName" };
                yield return new string[] { $"https://localhost/account/setpassword/{user}/sometoken", $"https://localhost/account/setpassword/ObfuscatedUserName/sometoken" };
                yield return new string[] { $"https://localhost/account/forgotpassword/{user}/sometoken", $"https://localhost/account/forgotpassword/ObfuscatedUserName/sometoken" };
            }
        }

        public static List<string> GenerateUserNames()
        {
            return new List<string>{ "user1", "user.1", "user_1", "user-1"};
        }
    }
}
