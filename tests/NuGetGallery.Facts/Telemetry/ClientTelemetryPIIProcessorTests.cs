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
using Xunit;

namespace NuGetGallery.Telemetry
{
    public class ClientTelemetryPIIProcessorTests
    {
        private RouteCollection _currentRoutes;

        public ClientTelemetryPIIProcessorTests()
        {
            if (_currentRoutes == null)
            {
                _currentRoutes = new RouteCollection();
                Routes.RegisterApiV2Routes(_currentRoutes);
                Routes.RegisterUIRoutes(_currentRoutes, adminPanelEnabled: true);
            }
        }

        [Fact]
        public void NullTelemetryItemDoesNotThrow()
        {
            // Arange
            var piiProcessor = CreatePIIProcessor();

            // Act
            piiProcessor.Process(null);
        }

        [Fact]
        public void NullRequestTelemetryUrlDoesNotThrow()
        {
            // Arange
            var piiProcessor = CreatePIIProcessor();
            var requestTelemetry = new RequestTelemetry
            {
                Url = null
            };

            // Act
            piiProcessor.Process(requestTelemetry);
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
            Assert.Empty(existentPIIOperations.Except(piiOperationsFromRoutes));
            Assert.Empty(piiOperationsFromRoutes.Except(existentPIIOperations));
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

        [Fact]
        public void PIIUrlDataGeneratorHasAllRoutes()
        {
            // Arange
            var piiUrlRoutes = GetPIIRoutesUrls();
            var generatorRoutes = PIIUrlDataGenerator()
                .Select(x => x[0])
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            // Act and Assert
            Assert.Empty(generatorRoutes.Except(piiUrlRoutes));
            Assert.Empty(piiUrlRoutes.Except(generatorRoutes));
        }

        [Theory]
        [MemberData(nameof(ObfuscatesGravatarUrlData))]
        public void ObfuscatesGravatarUrl(DependencyTelemetry input, string expectedData, string expectedName)
        {
            var target = CreatePIIProcessor();

            target.Process(input);

            Assert.Equal(expectedData, input.Data);
            Assert.Equal(expectedName, input.Name);
        }

        public static IEnumerable<object[]> ObfuscatesGravatarUrlData()
        {
            object[] ObfuscatesGravatarUrlData(
                string inputType = "HTTP",
                string inputData = null,
                string inputName = null,
                string expectedData = null,
                string expectedName = null)
            {
                return new object[]
                {
                    new DependencyTelemetry
                    {
                        Type = inputType,
                        Data = inputData,
                        Name = inputName,
                    },

                    expectedData,
                    expectedName,
                };
            }

            // Hashed email addresses are obfuscated from Gravatar URLs.
            yield return ObfuscatesGravatarUrlData(
                inputData: "http://gravatar.com/avatar/abc",
                inputName: "GET /avatar/abc",
                expectedData: "http://gravatar.com/avatar/Obfuscated",
                expectedName: "GET /avatar/Obfuscated");
            yield return ObfuscatesGravatarUrlData(
                inputData: "https://secure.gravatar.com/avatar/abc",
                inputName: "GET /avatar/abc",
                expectedData: "https://secure.gravatar.com/avatar/Obfuscated",
                expectedName: "GET /avatar/Obfuscated");
            yield return ObfuscatesGravatarUrlData(
                inputData: "https://secure.gravatar.com:443/avatar/abc",
                inputName: "GET /avatar/abc",
                expectedData: "https://secure.gravatar.com/avatar/Obfuscated",
                expectedName: "GET /avatar/Obfuscated");
            yield return ObfuscatesGravatarUrlData(
                inputData: "https://gravatar.com/avatar/abc?s=512&d=retro",
                inputName: "GET /avatar/abc",
                expectedData: "https://gravatar.com/avatar/Obfuscated?s=512&d=retro",
                expectedName: "GET /avatar/Obfuscated");
            yield return ObfuscatesGravatarUrlData(
                inputData: "https://gravatar.com/avatar/weird/url/but/whatever",
                inputName: "GET /avatar/abc",
                expectedData: "https://gravatar.com/avatar/Obfuscated",
                expectedName: "GET /avatar/Obfuscated");

            // Casing of the telemetry type should not matter.
            yield return ObfuscatesGravatarUrlData(
                inputType: "Http",
                inputData: "http://gravatar.com/avatar/abc",
                inputName: "GET /avatar/abc",
                expectedData: "http://gravatar.com/avatar/Obfuscated",
                expectedName: "GET /avatar/Obfuscated");

            // Unknown routes and invalid URLs are ignored
            yield return ObfuscatesGravatarUrlData(
                inputData: "https://gravatar.com/unknown/route",
                inputName: "GET /unknown/route",
                expectedData: "https://gravatar.com/unknown/route",
                expectedName: "GET /unknown/route");
            yield return ObfuscatesGravatarUrlData(
                inputData: "https://example.test/avatar/abc",
                inputName: "GET /avatar/abc",
                expectedData: "https://example.test/avatar/abc",
                expectedName: "GET /avatar/abc");
            yield return ObfuscatesGravatarUrlData(
                inputData: "avatar/abc",
                inputName: "GET /avatar/abc",
                expectedData: "avatar/abc",
                expectedName: "GET /avatar/abc");

            // The type must be "HTTP" for the data to be obfuscated
            yield return ObfuscatesGravatarUrlData(
                inputType: "Blob",
                inputData: "http://gravatar.com/avatar/abc",
                inputName: "GET /avatar/abc",
                expectedData: "http://gravatar.com/avatar/abc",
                expectedName: "GET /avatar/abc");
        }

        private ClientTelemetryPIIProcessor CreatePIIProcessor(string url = "")
        {
            return new TestClientTelemetryPIIProcessor(new TestProcessorNext(), url);
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

            public TestClientTelemetryPIIProcessor(ITelemetryProcessor next, string url) : base(next)
            {
                _url = url;
            }

            protected override Route GetCurrentRoute()
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
                return webRoute != null ? IsPIIUrl(webRoute.Url) : false;
            }).Select((r) =>
            {
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
                return webRoute != null ? IsPIIUrl(webRoute.Url) : false;
            }).Select((r) => ((Route)r).Url).Distinct().ToList();

            return piiRoutes;
        }

        private static bool IsPIIUrl(string url)
        {
            var hasPIIParameter = url.ToLower().Contains("token");

            var uri = new Uri(new Uri("https://localhost"), url);
            var isExcluded = uri.AbsolutePath == "/api/v2/token";

            return hasPIIParameter && !isExcluded;
        }

        public static IEnumerable<object[]> PIIUrlDataGenerator()
        {
            foreach (var user in GenerateUserNames())
            {
                yield return new string[] { "account/confirm/{accountName}/{token}", $"https://localhost/account/confirm/{user}/sometoken", $"https://localhost/account/confirm/{user}/ObfuscatedToken" };
                yield return new string[] { "account/forgotpassword/{username}/{token}", $"https://localhost/account/forgotpassword/{user}/sometoken", $"https://localhost/account/forgotpassword/{user}/ObfuscatedToken" };
                yield return new string[] { "account/setpassword/{username}/{token}", $"https://localhost/account/setpassword/{user}/sometoken", $"https://localhost/account/setpassword/{user}/ObfuscatedToken" };
                yield return new string[] { "account/transform/confirm/{accountNameToTransform}/{token}", $"https://localhost/account/transform/confirm/{user}/sometoken", $"https://localhost/account/transform/confirm/{user}/ObfuscatedToken" };
                yield return new string[] { "account/transform/reject/{accountNameToTransform}/{token}", $"https://localhost/account/transform/reject/{user}/sometoken", $"https://localhost/account/transform/reject/{user}/ObfuscatedToken" };
                yield return new string[] { "organization/{accountName}/members/confirm/{confirmationToken}", $"https://localhost/organization/{user}/members/confirm/sometoken", $"https://localhost/organization/{user}/members/confirm/ObfuscatedToken" };
                yield return new string[] { "organization/{accountName}/members/reject/{confirmationToken}", $"https://localhost/organization/{user}/members/reject/sometoken", $"https://localhost/organization/{user}/members/reject/ObfuscatedToken" };
                yield return new string[] { "packages/{id}/owners/{username}/cancel/{token}", $"https://localhost/packages/pack1/owners/{user}/cancel/sometoken", $"https://localhost/packages/pack1/owners/{user}/cancel/ObfuscatedToken" };
                yield return new string[] { "packages/{id}/owners/{username}/confirm/{token}", $"https://localhost/packages/pack1/owners/{user}/confirm/sometoken", $"https://localhost/packages/pack1/owners/{user}/confirm/ObfuscatedToken" };
                yield return new string[] { "packages/{id}/owners/{username}/reject/{token}", $"https://localhost/packages/pack1/owners/{user}/reject/sometoken", $"https://localhost/packages/pack1/owners/{user}/reject/ObfuscatedToken" };
            }

            yield return new string[] { "account/transform/cancel/{token}", "https://localhost/account/transform/cancel/sometoken", "https://localhost/account/transform/cancel/ObfuscatedToken" };
        }

        public static List<string> GenerateUserNames()
        {
            return new List<string> { "user1", "user.1", "user_1", "user-1" };
        }
    }
}
