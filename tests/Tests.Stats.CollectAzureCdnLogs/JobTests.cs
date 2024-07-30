// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using Microsoft.Extensions.Options;
using Moq;
using Stats.CollectAzureCdnLogs;
using Xunit;

namespace Tests.Stats.CollectAzureCdnLogs
{
    public class JobTests
    {
        private static IServiceContainer ServiceContainer = new ServiceContainer();

        [Fact]
        public void InitFailsWhenNoArguments()
        {
            var job = new Job();
            Assert.ThrowsAny<Exception>(() => job.Init(ServiceContainer, null));
        }

        [Fact]
        public void InitFailsWhenEmptyArguments()
        {
            var jobArgsDictionary = new Dictionary<string, string>();

            var job = new Job();
            Assert.ThrowsAny<Exception>(() => job.Init(ServiceContainer, jobArgsDictionary));
        }

        [Fact]
        public void InitSucceedsWhenValidConfiguration()
        {
            var job = new Job();
            var configuration = GetDefaultConfiguration();

            job.InitializeJobConfiguration(GetMockServiceProvider(configuration));
        }

        [Theory]
        // null values
        [InlineData("AzureCdnAccountNumber", null, typeof(ArgumentException))]
        [InlineData("AzureCdnCloudStorageAccount", null, typeof(ArgumentException))]
        [InlineData("AzureCdnCloudStorageContainerName", null, typeof(ArgumentException))]
        [InlineData("AzureCdnPlatform", null, typeof(ArgumentException))]
        [InlineData("FtpSourceUri", null, typeof(ArgumentException))]
        [InlineData("FtpSourceUsername", null, typeof(ArgumentException))]
        [InlineData("FtpSourcePassword", null, typeof(ArgumentException))]
        // empty values
        [InlineData("AzureCdnAccountNumber", "", typeof(ArgumentException))]
        [InlineData("AzureCdnCloudStorageAccount", "", typeof(ArgumentException))]
        [InlineData("AzureCdnCloudStorageContainerName", "", typeof(ArgumentException))]
        [InlineData("AzureCdnPlatform", "", typeof(ArgumentException))]
        [InlineData("FtpSourceUri", "", typeof(ArgumentException))]
        [InlineData("FtpSourceUsername", "", typeof(ArgumentException))]
        [InlineData("FtpSourcePassword", "", typeof(ArgumentException))]
        // invalid values
        [InlineData("FtpSourceUri", "http://localhost", typeof(UriFormatException))]
        [InlineData("FtpSourceUri", "ftps://someserver/folder", typeof(UriFormatException))]
        [InlineData("FtpSourceUri", "ftp://", typeof(UriFormatException))]
        [InlineData("AzureCdnPlatform", "bla", typeof(ArgumentException))]
        [InlineData("AzureCdnCloudStorageAccount", "bla", typeof(ArgumentException))]
        public void InitFailsWhenInvalidConfiguration(string property, object value, Type exceptionType)
        {
            var job = new Job();
            var configuration = GetModifiedConfiguration(property, value);

            Assert.Throws(exceptionType, () => job.InitializeJobConfiguration(GetMockServiceProvider(configuration)));
        }

        private static CollectAzureCdnLogsConfiguration GetModifiedConfiguration(string property, object value)
        {
            var configuration = GetDefaultConfiguration();

            typeof(CollectAzureCdnLogsConfiguration)
                .GetProperty(property)
                .SetValue(configuration, value);

            return configuration;
        }

        private static CollectAzureCdnLogsConfiguration GetDefaultConfiguration()
        {
            return new CollectAzureCdnLogsConfiguration
            {
                AzureCdnAccountNumber = "AA00",
                AzureCdnCloudStorageAccount = "UseDevelopmentStorage=true;",
                AzureCdnCloudStorageContainerName = "cdnLogs",
                AzureCdnPlatform = "HttpLargeObject",
                FtpSourceUri = "ftp://someserver/logFolder",
                FtpSourceUsername = @"domain\alias",
                FtpSourcePassword = "secret"
            };
        }

        private static IServiceProvider GetMockServiceProvider(CollectAzureCdnLogsConfiguration configuration)
        {
            var mockOptionsSnapshot = new Mock<IOptionsSnapshot<CollectAzureCdnLogsConfiguration>>();

            mockOptionsSnapshot
                .Setup(x => x.Value)
                .Returns(configuration);

            var mockProvider = new Mock<IServiceProvider>();

            mockProvider
                .Setup(sp => sp.GetService(It.IsAny<Type>()))
                .Returns<Type>(serviceType =>
                {
                    if (serviceType == typeof(IOptionsSnapshot<CollectAzureCdnLogsConfiguration>))
                    {
                        return mockOptionsSnapshot.Object;
                    }
                    else if (serviceType == typeof(ITelemetryService))
                    {
                        return Mock.Of<ITelemetryService>();
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unexpected service lookup: {serviceType.Name}");
                    }
                });

            return mockProvider.Object;
        }

        [Theory]
        [InlineData("123 0 - - - 443 - - - - - 0 100 - - 123 -")]
        [InlineData("123 0 \"-\" \"-\" \"-\" 443 \"-\" \"-\" \"-\" \"-\" \"-\" 0 100 \"-\" \"-\" 123 \"-\"")]
        public void HandlesEmptyW3CValues(string input)
        {
            var job = new Job();
            var output = job.GetParsedModifiedLogEntry(
                0,
                input,
                "foo.log");

            Assert.Equal("123 0 - - - 443 - - - - - 0 100 - - 123 -\r\n", output);
        }
    }
}