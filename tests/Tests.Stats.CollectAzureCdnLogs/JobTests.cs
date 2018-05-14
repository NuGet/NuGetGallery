// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
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
        public void InitSucceedsWhenValidArguments()
        {
            var jobArgsDictionary = CreateValidJobArgsDictionary();

            var job = new Job();
            job.Init(ServiceContainer, jobArgsDictionary);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("http://localhost")]
        [InlineData("ftps://someserver/folder")]
        [InlineData("ftp://")]
        public void InitFailsForInvalidFtpServerUri(string serverUri)
        {
            var jobArgsDictionary = CreateValidJobArgsDictionary();
            jobArgsDictionary["FtpSourceUri"] = serverUri;

            var job = new Job();
            Assert.ThrowsAny<Exception>(() => job.Init(ServiceContainer, jobArgsDictionary));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void InitFailsForMissingFtpUsername(string username)
        {
            var jobArgsDictionary = CreateValidJobArgsDictionary();
            jobArgsDictionary["FtpSourceUsername"] = username;

            var job = new Job();
            Assert.ThrowsAny<Exception>(() => job.Init(ServiceContainer, jobArgsDictionary));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void InitFailsForMissingFtpPassword(string password)
        {
            var jobArgsDictionary = CreateValidJobArgsDictionary();
            jobArgsDictionary["FtpSourcePassword"] = password;

            var job = new Job();
            Assert.ThrowsAny<Exception>(() => job.Init(ServiceContainer, jobArgsDictionary));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("bla")]
        public void InitFailsForMissingOrInvalidAzureCdnPlatform(string platform)
        {
            var jobArgsDictionary = CreateValidJobArgsDictionary();
            jobArgsDictionary["AzureCdnPlatform"] = platform;

            var job = new Job();
            Assert.ThrowsAny<Exception>(() => job.Init(ServiceContainer, jobArgsDictionary));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void InitFailsForMissingAzureCdnAccountNumber(string accountNumber)
        {
            var jobArgsDictionary = CreateValidJobArgsDictionary();
            jobArgsDictionary["AzureCdnAccountNumber"] = accountNumber;

            var job = new Job();
            Assert.ThrowsAny<Exception>(() => job.Init(ServiceContainer, jobArgsDictionary));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("bla")]
        public void InitFailsForMissingOrInvalidAzureCdnCloudStorageAccount(string cloudStorageAccount)
        {
            var jobArgsDictionary = CreateValidJobArgsDictionary();
            jobArgsDictionary["AzureCdnCloudStorageAccount"] = cloudStorageAccount;

            var job = new Job();
            Assert.ThrowsAny<Exception>(() => job.Init(ServiceContainer, jobArgsDictionary));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void InitFailsForMissingAzureCdnCloudStorageContainerName(string containerName)
        {
            var jobArgsDictionary = CreateValidJobArgsDictionary();
            jobArgsDictionary["AzureCdnCloudStorageContainerName"] = containerName;

            var job = new Job();
            Assert.ThrowsAny<Exception>(() => job.Init(ServiceContainer, jobArgsDictionary));
        }

        private static Dictionary<string, string> CreateValidJobArgsDictionary()
        {
            var jobArgsDictionary = new Dictionary<string, string>();
            jobArgsDictionary.Add("FtpSourceUri", "ftp://someserver/logFolder");
            jobArgsDictionary.Add("FtpSourceUsername", @"domain\alias");
            jobArgsDictionary.Add("FtpSourcePassword", "secret");
            jobArgsDictionary.Add("AzureCdnPlatform", "HttpLargeObject");
            jobArgsDictionary.Add("AzureCdnAccountNumber", "AA00");
            jobArgsDictionary.Add("AzureCdnCloudStorageAccount", "UseDevelopmentStorage=true;");
            jobArgsDictionary.Add("AzureCdnCloudStorageContainerName", "cdnLogs");

            return jobArgsDictionary;
        }
    }
}