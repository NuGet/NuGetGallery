// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Storage.Blobs;
using Azure;
using Moq;
using Stats.AzureCdnLogs.Common.Collect;
using Stats.AzureCdnLogs.Common;
using Stats.CollectAzureChinaCDNLogs;
using Xunit;
using NuGet.Jobs;

namespace Tests.Stats.CollectAzureChinaCDNLogs
{
    public class JobTests
    {
        private static Mock<IServiceContainer> MockServiceContainer = new Mock<IServiceContainer>();

        [Fact]
        public void InitFailsWhenEmptyArguments()
        {
            var jobArgsDictionary = new Dictionary<string, string>();

            var job = new Job();
            Assert.ThrowsAny<ArgumentNullException>(() => job.Init(MockServiceContainer.Object, jobArgsDictionary));
        }

        [Fact]
        public void InitFailsWithInvalidAccount()
        {
            var job = new Job();
            var configuration = GetDefaultConfiguration();
            var msiConfiguration = GetDefaultStorageMsiConfiguration();

            var ex = Assert.ThrowsAny<AggregateException>(() => job.InitializeJobConfiguration(GetMockServiceProvider(configuration, msiConfiguration)));
            Assert.IsType<RequestFailedException>(ex.InnerException);
        }

        [Theory]
        // null values
        [InlineData("AzureAccountConnectionStringSource", null, typeof(ArgumentException))]
        [InlineData("AzureAccountConnectionStringDestination", null, typeof(ArgumentException))]
        [InlineData("AzureContainerNameDestination", null, typeof(ArgumentNullException))]
        [InlineData("AzureContainerNameSource", null, typeof(ArgumentNullException))]
        [InlineData("DestinationFilePrefix", null, typeof(AggregateException))]
        // empty values
        [InlineData("AzureAccountConnectionStringSource", "", typeof(ArgumentException))]
        [InlineData("AzureAccountConnectionStringDestination", "", typeof(ArgumentException))]
        [InlineData("AzureContainerNameDestination", "", typeof(ArgumentException))]
        [InlineData("AzureContainerNameSource", "", typeof(ArgumentException))]
        [InlineData("DestinationFilePrefix", "", typeof(AggregateException))]
        public void InitMissingArgArguments(string property, object value, Type exceptionType)
        {
            var job = new Job();
            var configuration = GetModifiedConfiguration(property, value);
            var msiConfiguration = GetDefaultStorageMsiConfiguration();

            Assert.Throws(exceptionType, () => job.InitializeJobConfiguration(GetMockServiceProvider(configuration, msiConfiguration)));
        }

        private static CollectAzureChinaCdnLogsConfiguration GetModifiedConfiguration(string property, object value)
        {
            var configuration = GetDefaultConfiguration();

            typeof(CollectAzureChinaCdnLogsConfiguration)
                .GetProperty(property)
                .SetValue(configuration, value);

            return configuration;
        }

        private static CollectAzureChinaCdnLogsConfiguration GetDefaultConfiguration()
        {
            return new CollectAzureChinaCdnLogsConfiguration
            {
                // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="Test secret")]
                AzureAccountConnectionStringSource = "DefaultEndpointsProtocol=https;AccountName=thisstorageaccountnameistoolong;AccountKey=cdummy4aadummyAAWhdummyAdummyA6A+dummydoAdummyJqdummymnm+H+2dummyA/dummygdummyqdummyKK==;EndpointSuffix=core.chinacloudapi.cn",
                // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="Test secret")]
                AzureAccountConnectionStringDestination = "DefaultEndpointsProtocol=https;AccountName=thisstorageaccountnameistoolong;AccountKey=cdummy4aadummyAAWhdummyAdummyA6A+dummydoAdummyJqdummymnm+H+2dummyA/dummygdummyqdummyKK==;EndpointSuffix=core.windows.net",
                AzureContainerNameDestination = "DestContainer",
                AzureContainerNameSource = "SourceContainer",
                DestinationFilePrefix = "SomePrfix",
                ExecutionTimeoutInSeconds = 60
            };
        }

        private static StorageMsiConfiguration GetDefaultStorageMsiConfiguration()
        {
            return new StorageMsiConfiguration
            {
                UseManagedIdentity = false,
                ManagedIdentityClientId = "dummy"
            };
        }

        private static IServiceProvider GetMockServiceProvider(CollectAzureChinaCdnLogsConfiguration configuration, StorageMsiConfiguration msiConfiguration)
        {
            var mockOptionsSnapshot = new Mock<IOptionsSnapshot<CollectAzureChinaCdnLogsConfiguration>>();
            var mockOptionsSnapshotStorageMsi = new Mock<IOptionsSnapshot<StorageMsiConfiguration>>();
            var logger_AzureBlobLeaseManager = new Mock<ILogger<AzureBlobLeaseManager>>();
            var logger_AzureStatsLogSource = new Mock<ILogger<AzureStatsLogSource>>();
            var logger_AzureStatsLogDestination = new Mock<ILogger<AzureStatsLogDestination>>();

            mockOptionsSnapshot
                .Setup(x => x.Value)
                .Returns(configuration);

            mockOptionsSnapshotStorageMsi
                 .Setup(x => x.Value)
                .Returns(msiConfiguration);

            var mockProvider = new Mock<IServiceProvider>();

            mockProvider
                .Setup(sp => sp.GetService(It.IsAny<Type>()))
                .Returns<Type>(serviceType =>
                {
                    if (serviceType == typeof(IOptionsSnapshot<CollectAzureChinaCdnLogsConfiguration>))
                    {
                        return mockOptionsSnapshot.Object;
                    }
                    if (serviceType == typeof(IOptionsSnapshot<StorageMsiConfiguration>))
                    {
                        return mockOptionsSnapshotStorageMsi.Object;
                    }
                    else if (serviceType == typeof(ILogger<AzureBlobLeaseManager>))
                    {
                        return logger_AzureBlobLeaseManager.Object;
                    }
                    else if (serviceType == typeof(ILogger<AzureStatsLogSource>))
                    {
                        return logger_AzureStatsLogSource.Object;
                    }
                    else if (serviceType == typeof(ILogger<AzureStatsLogDestination>))
                    {
                        return logger_AzureStatsLogDestination.Object;
                    }
                    else 
                    {
                        throw new InvalidOperationException($"Unexpected service lookup: {serviceType.Name}");
                    }
                });

            return mockProvider.Object;
        }
    }
}
