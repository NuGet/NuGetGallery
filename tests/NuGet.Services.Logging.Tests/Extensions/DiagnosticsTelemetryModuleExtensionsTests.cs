// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing;
using Xunit;

namespace NuGet.Services.Logging.Tests
{
    public class DiagnosticsTelemetryModuleExtensionsTests
    {
        public class TheAddOrSetHeartbeatPropertyMethod
        {
            [Fact]
            public void ThrowsForNullModule()
            {
                Assert.Throws<ArgumentNullException>(
                    "module",
                    () => DiagnosticsTelemetryModuleExtensions.AddOrSetHeartbeatProperty(null, "name", "value", true));
            }

            [Fact]
            public void ReturnsFalseForNullPropertyName()
            {
                // Arrange
                var module = new DiagnosticsTelemetryModule();

                // Act 
                var result = DiagnosticsTelemetryModuleExtensions.AddOrSetHeartbeatProperty(module, null, "value", true);

                // Assert
                Assert.False(result);
            }

            [Theory]
            [InlineData("propertyName", "propertyValue", true)]
            [InlineData("propertyName", "propertyValue", false)]
            public void ReturnsTrueAndAddsPayloadWhenNotAddedYet(string propertyName, string propertyValue, bool isHealthy)
            {
                // Arrange
                var module = new DiagnosticsTelemetryModule();

                // Act 
                var result = DiagnosticsTelemetryModuleExtensions.AddOrSetHeartbeatProperty(
                    module,
                    propertyName,
                    propertyValue,
                    isHealthy);

                // Assert
                Assert.True(result);

                VerifyHeartbeatPropertyPayload(module, propertyName, propertyValue, isHealthy);
            }

            [Theory]
            [InlineData("propertyName", "propertyValue", true)]
            [InlineData("propertyName", "propertyValue", false)]
            public void ReturnsTrueAndSetsPayloadWhenAlreadyAdded(string propertyName, string propertyValue, bool isHealthy)
            {
                // Arrange
                var module = new DiagnosticsTelemetryModule();
                module.AddHeartbeatProperty(propertyName, propertyValue, isHealthy);

                // Act 
                var result = DiagnosticsTelemetryModuleExtensions.AddOrSetHeartbeatProperty(
                    module,
                    propertyName,
                    propertyValue,
                    isHealthy);

                // Assert
                Assert.True(result);

                VerifyHeartbeatPropertyPayload(module, propertyName, propertyValue, isHealthy);
            }

            private void VerifyHeartbeatPropertyPayload(
                DiagnosticsTelemetryModule module,
                string propertyName,
                string propertyValue,
                bool isHealthy)
            {
                // Verify payload settings using reflection
                var heartbeatPropertyManager = typeof(DiagnosticsTelemetryModule)
                    .GetField("HeartbeatProvider", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(module) as IHeartbeatPropertyManager;

                var heartbeatProperties = heartbeatPropertyManager
                    .GetType()
                    .GetField("heartbeatProperties", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(heartbeatPropertyManager); // returns ConcurrentDictionary<string, HeartbeatPropertyPayload>

                var propertyNames = heartbeatProperties
                    .GetType()
                    .GetProperty("Keys", BindingFlags.Public | BindingFlags.Instance)
                    .GetValue(heartbeatProperties) as ICollection<string>;

                Assert.Contains(propertyNames, i => i.Equals(propertyName));

                var propertyPayload = heartbeatProperties
                    .GetType()
                    .GetProperty("Item", BindingFlags.Public | BindingFlags.Instance)
                    .GetValue(heartbeatProperties, new object[] { propertyName }); // returns internal type HeartbeatPropertyPayload

                var payloadValue = propertyPayload
                    .GetType()
                    .GetProperty("PayloadValue", BindingFlags.Public | BindingFlags.Instance)
                    .GetValue(propertyPayload) as string;

                var payloadIsHealthy = (bool)propertyPayload
                    .GetType()
                    .GetProperty("IsHealthy", BindingFlags.Public | BindingFlags.Instance)
                    .GetValue(propertyPayload);

                Assert.Equal(propertyValue, payloadValue);
                Assert.Equal(isHealthy, payloadIsHealthy);
            }
        }
    }
}
