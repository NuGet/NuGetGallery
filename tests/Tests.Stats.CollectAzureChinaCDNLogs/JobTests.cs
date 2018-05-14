// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using Microsoft.WindowsAzure.Storage;
using Stats.CollectAzureChinaCDNLogs;
using Xunit;

namespace Tests.Stats.CollectAzureChinaCDNLogs
{
    public class JobTests
    {
        private static IServiceContainer ServiceContainer = new ServiceContainer();

        [Fact]
        public void InitFailsWhenEmptyArguments()
        {
            var jobArgsDictionary = new Dictionary<string, string>();

            var job = new Job();
            Assert.ThrowsAny<Exception>(() => job.Init(ServiceContainer, jobArgsDictionary));
        }

        [Fact]
        public void InitFailsWithInvalidAccount()
        {
            var jobArgsDictionary = CreateValidJobArgsDictionary();

            var job = new Job();
            Assert.ThrowsAny<StorageException>(() => job.Init(ServiceContainer, jobArgsDictionary));
        }

        [Theory]
        [InlineData("AzureAccountConStringSource")]
        [InlineData("AzureAccountConStringDest")]
        [InlineData("AzureContainerNameDest")]
        [InlineData("AzureContainerNameSource")]
        [InlineData("DestinationFilePrefix")]
        public void InitMissingArgArguments(string keyToRemove)
        {
            var jobArgsDictionary = CreateValidJobArgsDictionary();
            jobArgsDictionary.Remove(keyToRemove);
            var job = new Job();
            Assert.ThrowsAny<Exception>(() => job.Init(ServiceContainer, jobArgsDictionary));
        }


        private static Dictionary<string, string> CreateValidJobArgsDictionary()
        {
            var jobArgsDictionary = new Dictionary<string, string>();
            jobArgsDictionary.Add("AzureAccountConnectionStringSource", "DefaultEndpointsProtocol=https;AccountName=name;AccountKey=cdummy4aadummyAAWhdummyAdummyA6A+dummydoAdummyJqdummymnm+H+2dummyA/dummygdummyqdummyKK==;EndpointSuffix=core.chinacloudapi.cn");
            jobArgsDictionary.Add("AzureAccountConnectionStringDestination", "DefaultEndpointsProtocol=https;AccountName=name;AccountKey=cdummy4aadummyAAWhdummyAdummyA6A+dummydoAdummyJqdummymnm+H+2dummyA/dummygdummyqdummyKK==;EndpointSuffix=core.windows.net");
            jobArgsDictionary.Add("AzureContainerNameDestination", "DestContainer");
            jobArgsDictionary.Add("AzureContainerNameSource", "SourceContainer");
            jobArgsDictionary.Add("DestinationFilePrefix", "SomePrfix");
            jobArgsDictionary.Add("ExecutionTimeoutInSeconds", "60");

            return jobArgsDictionary;
        }

    }
}
