// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using Xunit;

namespace NuGet.Services.AzureManagement.Tests
{
    public class AzureHelperTests
    {
        [Fact]
        public void ParseCloudServiceProperties()
        {
            // Arrange
            var content = ReadFile("CloudServiceProperties.json");

            // Act
            var cloudService = AzureHelper.ParseCloudServiceProperties(content);

            // Assert
            Assert.Equal("http://nuget-dev-0-v2v3search.cloudapp.net/", cloudService.Uri.AbsoluteUri);
            Assert.Equal(3, cloudService.InstanceCount);
        }

        [Fact]
        public void ParseTrafficManagerProperties()
        {
            // Arrange
            var content = ReadFile("TrafficManagerProperties.json");

            // Act
            var trafficManager = AzureHelper.ParseTrafficManagerProperties(content);

            // Assert
            Assert.Equal("dev-nuget-tfm.trafficmanager.net", trafficManager.Domain);
            Assert.Equal("/api/status", trafficManager.Path);

            var endpoints = trafficManager.Endpoints;

            var endpoint1 = endpoints.First();
            AssertEndpoint(endpoint1, "Endpoint1", "nuget-test-trafficmanager.cloudapp.net", AzureHelper.TrafficManagerEndpointStatus.Online, AzureHelper.TrafficManagerEndpointProbeStatus.Enabled);

            var endpoint2 = endpoints.Skip(1).First();
            AssertEndpoint(endpoint2, "Endpoint2", "nuget-test-trafficmanager2.cloudapp.net", AzureHelper.TrafficManagerEndpointStatus.Online, AzureHelper.TrafficManagerEndpointProbeStatus.Enabled);
        }

        private string ReadFile(string filename)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), filename);
            return File.ReadAllText(path);
        }

        private void AssertEndpoint(AzureHelper.TrafficManagerEndpointProperties endpoint,
            string name,
            string target,
            AzureHelper.TrafficManagerEndpointStatus status,
            AzureHelper.TrafficManagerEndpointProbeStatus probeStatus)
        {
            Assert.Equal(name, endpoint.Name);
            Assert.Equal(target, endpoint.Target);
            Assert.Equal(status, endpoint.Status);
            Assert.Equal(probeStatus, endpoint.ProbeStatus);
        }
    }
}
