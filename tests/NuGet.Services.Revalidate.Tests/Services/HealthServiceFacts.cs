// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Revalidate.Tests.TestData;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.Revalidate.Tests.Services
{
    public class HealthServiceFacts
    {
        private readonly Mock<ICoreFileStorageService> _storage;
        private readonly HealthConfiguration _config;
        private readonly HealthService _target;

        public HealthServiceFacts()
        {
            _storage = new Mock<ICoreFileStorageService>();
            _config = new HealthConfiguration
            {
                ContainerName = "status",
                StatusBlobName = "status.json",
                ComponentPath = "NuGet/Package Publishing"
            };

            _target = new HealthService(_storage.Object, _config, Mock.Of<ILogger<HealthService>>());
        }

        [Theory]
        [InlineData(TestResources.PackagePublishingDegradedStatus, false)]
        [InlineData(TestResources.PackagePublishingDownStatus, false)]
        [InlineData(TestResources.PackagePublishingUpStatus, true)]
        public async Task ReturnsHealthyIfStatusBlobIndicatesHealthyComponent(string resourceName, bool expectsHealthy)
        {
            _storage
                .Setup(s => s.GetFileAsync(_config.ContainerName, _config.StatusBlobName))
                .ReturnsAsync(TestResources.GetResourceStream(resourceName));

            Assert.Equal(expectsHealthy, await _target.IsHealthyAsync());
        }

        [Fact]
        public async Task AssumesUnhealthyIfComponentCannotBeFoundInStatusBlob()
        {
            _storage
                .Setup(s => s.GetFileAsync(_config.ContainerName, _config.StatusBlobName))
                .ReturnsAsync(TestResources.GetResourceStream(TestResources.PackagePublishingMissingStatus));

            Assert.False(await _target.IsHealthyAsync());
        }

        [Fact]
        public async Task ThrowsIfStorageServiceThrows()
        {
            // This may happen if the status blob can't be found.
            var expectedException = new Exception("Look ma, I'm an exception!");

            _storage
                .Setup(s => s.GetFileAsync(_config.ContainerName, _config.StatusBlobName))
                .ThrowsAsync(expectedException);

            // Act & Assert
            var actualException = await Assert.ThrowsAsync<Exception>(() => _target.IsHealthyAsync());

            Assert.Same(expectedException, actualException);
        }
    }
}
