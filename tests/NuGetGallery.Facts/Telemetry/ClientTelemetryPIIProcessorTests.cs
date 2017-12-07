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
            string userName = "user1";
            var piiProcessor = CreatePIIProcessor(false, userName);

            // Act
            piiProcessor.Process(null);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void UrlIsUpdatedOnPIIAction(bool actionIsPII)
        {
            // Arange
            string userName = "user1";
            var piiProcessor = CreatePIIProcessor(actionIsPII, userName);
            RequestTelemetry telemetryItem = new RequestTelemetry();
            telemetryItem.Url = new Uri("https://localhost/user1");

            // Act
            piiProcessor.Process(telemetryItem);

            // Assert
            string expected = actionIsPII ? $"https://localhost/{Obfuscator.DefaultTelemetryUserName}" : telemetryItem.Url.ToString();
            Assert.Equal(expected, telemetryItem.Url.ToString());
        }

        [Theory]
        [MemberData(nameof(PIIOperationDataGenerator))]
        public void TestValidPIIOperations(string operation)
        {
            // Arange
            var piiProcessor = (TestClientTelemetryPIIProcessor)CreatePIIProcessor(false, "user");

            // Act and Assert
            Assert.True(piiProcessor.IsPIIOperationBase(operation));
        }

        [Theory]
        [MemberData(nameof(InvalidPIIOPerationDataGenerator))]
        public void TestInvalidPIIOperations(string operation)
        {
            // Arange
            var piiProcessor = (TestClientTelemetryPIIProcessor)CreatePIIProcessor(false, "user");

            // Act and Assert
            Assert.False(piiProcessor.IsPIIOperationBase(operation));
        }

        [Fact]
        public void ValidatePIIRoutes()
        {
            // Arange
            HashSet<string> existentPIIOperations = Obfuscator.PIIActions;
            List<string> piiOperationsFromRoutes = GetPIIOperationsFromRoute();

            // Act and Assert
            Assert.True(existentPIIOperations.SetEquals(piiOperationsFromRoutes));
        }

        private ClientTelemetryPIIProcessor CreatePIIProcessor(bool isPIIOperation, string userName)
        {
            return new TestClientTelemetryPIIProcessor(new TestProcessorNext(), isPIIOperation, userName);
        }

        private class TestProcessorNext : ITelemetryProcessor
        {
            public void Process(ITelemetry item)
            {
            }
        }

        private class TestClientTelemetryPIIProcessor : ClientTelemetryPIIProcessor
        {
            private User _testUser;
            private bool _isPIIOperation;

            public TestClientTelemetryPIIProcessor(ITelemetryProcessor next, bool isPIIOperation, string userName) : base(next)
            {
                _isPIIOperation = isPIIOperation;
                _testUser = new User(userName);
            }

            protected override bool IsPIIOperation(string operationName)
            {
                return _isPIIOperation;
            }

            public bool IsPIIOperationBase(string operationName)
            {
                return base.IsPIIOperation(operationName);
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
            }).Select((r)=> {
                var dd = ((Route)r).Defaults;
                return $"{dd["controller"]}/{dd["action"]}";
            }).Distinct().ToList();

            return piiRoutes;
        }

        private static bool IsPIIUrl(string url)
        {
            return url.ToLower().Contains("username") || url.ToLower().Contains("accountname");
        }

        public static IEnumerable<string[]> PIIOperationDataGenerator()
        {
            return GetPIIOperationsFromRoute().Select(o => new string[]{$"GET {o}"});
        }

        public static IEnumerable<string[]> InvalidPIIOPerationDataGenerator()
        {
            yield return new string[]{ null };
            yield return new string[]{ string.Empty };
            yield return new string[]{"Some random data" };
        }
    }
}
