// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Moq;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;
using Xunit;

namespace NuGetGallery.Services
{
    public class StatusServiceFacts
    {
        [Fact]
        public void ValidateServicesThatWillBeInStatusValidation()
        {
            // Arrange
            var cloudAuditingServiceIsStatusParticipant = typeof(ICloudStorageStatusDependency).IsAssignableFrom(typeof(CloudAuditingService));

            // Assert
            Assert.True(cloudAuditingServiceIsStatusParticipant);
        }

        public class TheIsAzureStorageAvailable
        {
            [Fact]
            public async Task ArgumentCheckStorageTypeFileSystemReturnsNull()
            {
                // Arrange
                var appCopnfiguration = new Mock<IAppConfiguration>();
                appCopnfiguration.SetupGet(c => c.StorageType).Returns(StorageType.FileSystem);

                var cloudStorageStatusDependency1 = CloudStorageStatusDependencyFactory.GetCloudStorageStatusDependency(ICloudStorageStatusDependencyTestType.Available, appCopnfiguration.Object);

                var statusTestService = new StatusService(entities: null,
                    cloudStorageAvailabilityChecks: new ICloudStorageStatusDependency[] { cloudStorageStatusDependency1 },
                    config: appCopnfiguration.Object);
                var result = await statusTestService.IsAzureStorageAvailable();

                // Assert
                Assert.Null(result);
            }

            [Fact]
            public async Task ArgumentCheckNullConfigReturnsNull()
            {
                // Arrange
                var statusTestService = new StatusService(entities: null,
                    cloudStorageAvailabilityChecks: new ICloudStorageStatusDependency[] {},
                    config: null);
                var result = await statusTestService.IsAzureStorageAvailable();

                // Assert
                Assert.Null(result);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task AvailableStoragesReturnsAvailable(bool readOnly)
            {
                // Arrange
                var appCopnfiguration = new Mock<IAppConfiguration>();
                appCopnfiguration.SetupGet(c => c.ReadOnlyMode).Returns(readOnly);
                appCopnfiguration.SetupGet(c => c.StorageType).Returns(StorageType.AzureStorage);

                var cloudStorageStatusDependency1 = CloudStorageStatusDependencyFactory.GetCloudStorageStatusDependency(ICloudStorageStatusDependencyTestType.Available, appCopnfiguration.Object);
                var cloudStorageStatusDependency2 = CloudStorageStatusDependencyFactory.GetCloudStorageStatusDependency(ICloudStorageStatusDependencyTestType.Available, appCopnfiguration.Object);

                var statusTestService = new StatusService(entities: null,
                    cloudStorageAvailabilityChecks: new ICloudStorageStatusDependency[] { cloudStorageStatusDependency1, cloudStorageStatusDependency2 },
                    config: appCopnfiguration.Object);
                var result = await statusTestService.IsAzureStorageAvailable();

                // Assert
                Assert.True(result.Value);    
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task AtLeastOneNotAvailableStoragesReturnsNotAvailable(bool readOnly)
            {
                // Arrange
                var appCopnfiguration = new Mock<IAppConfiguration>();
                appCopnfiguration.SetupGet(c => c.ReadOnlyMode).Returns(readOnly);
                appCopnfiguration.SetupGet(c => c.StorageType).Returns(StorageType.AzureStorage);

                var cloudStorageStatusDependency1 = CloudStorageStatusDependencyFactory.GetCloudStorageStatusDependency(ICloudStorageStatusDependencyTestType.Available, appCopnfiguration.Object);
                var cloudStorageStatusDependency2 = CloudStorageStatusDependencyFactory.GetCloudStorageStatusDependency(ICloudStorageStatusDependencyTestType.NotAvailable, appCopnfiguration.Object);

                var statusTestService = new StatusService(entities: null,
                    cloudStorageAvailabilityChecks: new ICloudStorageStatusDependency[] { cloudStorageStatusDependency1, cloudStorageStatusDependency2 },
                    config: appCopnfiguration.Object);
                var result = await statusTestService.IsAzureStorageAvailable();

                // Assert
                Assert.False(result.Value);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task AtLeastOneStatusCheckThrowsExceptionStoragesReturnsNotAvailable(bool readOnly)
            {
                // Arrange
                var appCopnfiguration = new Mock<IAppConfiguration>();
                appCopnfiguration.SetupGet(c => c.ReadOnlyMode).Returns(readOnly);
                appCopnfiguration.SetupGet(c => c.StorageType).Returns(StorageType.AzureStorage);

                var cloudStorageStatusDependency1 = CloudStorageStatusDependencyFactory.GetCloudStorageStatusDependency(ICloudStorageStatusDependencyTestType.Available, appCopnfiguration.Object);
                var cloudStorageStatusDependency2 = CloudStorageStatusDependencyFactory.GetCloudStorageStatusDependency(ICloudStorageStatusDependencyTestType.ThrowingException, appCopnfiguration.Object);

                var statusTestService = new StatusService(entities: null,
                    cloudStorageAvailabilityChecks: new ICloudStorageStatusDependency[] { cloudStorageStatusDependency1, cloudStorageStatusDependency2 },
                    config: appCopnfiguration.Object);
                var result = await statusTestService.IsAzureStorageAvailable();

                // Assert
                Assert.False(result.Value);
            }

            private enum ICloudStorageStatusDependencyTestType
            {
                Available,
                NotAvailable,
                ThrowingException
            }

            private class CloudStorageStatusDependencyFactory
            {

                private CloudStorageStatusDependencyFactory()
                {
                }

                public static ICloudStorageStatusDependency GetCloudStorageStatusDependency(ICloudStorageStatusDependencyTestType type, IAppConfiguration config)
                {
                    switch(type)
                    {
                        case ICloudStorageStatusDependencyTestType.Available:
                            return new CloudStorageStatusDependencyIsAvailable(config);
                        case ICloudStorageStatusDependencyTestType.NotAvailable:
                            return new CloudStorageStatusDependencyIsNotAvailable(config);
                        case ICloudStorageStatusDependencyTestType.ThrowingException:
                            return new CloudStorageStatusDependencyThrows(config);
                        default:
                            throw new NotSupportedException(nameof(type));
                    }
                }

                private static void AssertConfigLocationMode(IAppConfiguration config, CloudBlobLocationMode? locationMode)
                {
                    if (config.ReadOnlyMode)
                    {
                        Assert.Equal(CloudBlobLocationMode.SecondaryOnly, locationMode.Value);
                    }
                    else
                    {
                        Assert.Equal(CloudBlobLocationMode.PrimaryOnly, locationMode.Value);
                    }
                }

                private class CloudStorageStatusDependencyIsAvailable : ICloudStorageStatusDependency
                {
                    private readonly IAppConfiguration _config;
                    public CloudStorageStatusDependencyIsAvailable(IAppConfiguration config)
                    {
                        _config = config;
                    }

                    public Task<bool> IsAvailableAsync(CloudBlobLocationMode? locationMode)
                    {
                        AssertConfigLocationMode(_config, locationMode);
                        return Task.FromResult(true);
                    }
                }

                private class CloudStorageStatusDependencyIsNotAvailable : ICloudStorageStatusDependency
                {
                    private readonly IAppConfiguration _config;

                    public CloudStorageStatusDependencyIsNotAvailable(IAppConfiguration config)
                    {
                        _config = config;
                    }

                    public Task<bool> IsAvailableAsync(CloudBlobLocationMode? locationMode)
                    {
                        AssertConfigLocationMode(_config, locationMode);
                        return Task.FromResult(false);
                    }
                }

                private class CloudStorageStatusDependencyThrows : ICloudStorageStatusDependency
                {
                    private readonly IAppConfiguration _config;

                    public CloudStorageStatusDependencyThrows(IAppConfiguration config)
                    {
                        _config = config;
                    }

                    public async Task<bool> IsAvailableAsync(CloudBlobLocationMode? locationMode)
                    {
                        AssertConfigLocationMode(_config, locationMode);
                        // Just to go async.
                        await Task.Yield();
                        throw new Exception("Boo"); 
                    }
                }
            }
        }
    }
}