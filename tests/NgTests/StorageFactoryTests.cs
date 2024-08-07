// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel;
using Ng;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace NgTests
{
    public class StorageFactoryTests
    {
        private const string DummyKey = "DummyDUMMYpZxLeDumMyyN52gJj+ZlGE0ipRi9PaTcn9AU4epwvsngE5rLSMk9TwpazxUtzeyBnFeWFAdummyw==";

        [Fact]
        public void AzureFactory_DefaultsToAzurePublicWithNoCompression()
        {
            // Arrange
            Dictionary<string, string> arguments = new Dictionary<string, string>()
            {
                { Arguments.StorageBaseAddress, "http://localhost/reg" },
                { Arguments.StorageAccountName, "testAccount" },
                { Arguments.StorageKeyValue, DummyKey },
                { Arguments.StorageContainer, "testContainer" },
                { Arguments.StoragePath, "testStoragePath" },
                { Arguments.StorageType, "azure" },
            };

            // Act
            StorageFactory factory = CommandHelpers.CreateStorageFactory(arguments, true);

            // Assert
            var azureFactory = Assert.IsType<AzureStorageFactory>(factory);
            Assert.False(azureFactory.CompressContent, "The azure storage factory should not compress the content.");
            Assert.Equal("https://testaccount.blob.core.windows.net/testContainer/testStoragePath/", azureFactory.DestinationAddress.AbsoluteUri);
        }

        [Fact]
        public void AzureFactory_AllowsCustomStorageSuffix()
        {
            // Arrange
            Dictionary<string, string> arguments = new Dictionary<string, string>()
            {
                { Arguments.StorageBaseAddress, "http://localhost/reg" },
                { Arguments.StorageAccountName, "testAccount" },
                { Arguments.StorageKeyValue, DummyKey },
                { Arguments.StorageContainer, "testContainer" },
                { Arguments.StoragePath, "testStoragePath" },
                { Arguments.StorageType, "azure" },
                { Arguments.StorageSuffix, "core.chinacloudapi.cn" },
            };

            // Act
            StorageFactory factory = CommandHelpers.CreateStorageFactory(arguments, true);

            // Assert
            var azureFactory = Assert.IsType<AzureStorageFactory>(factory);
            Assert.Equal("https://testaccount.blob.core.chinacloudapi.cn/testContainer/testStoragePath/", azureFactory.DestinationAddress.AbsoluteUri);
        }
    }
}
