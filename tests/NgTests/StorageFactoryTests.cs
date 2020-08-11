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
        [Theory]
        [Description("The regular azure factory should not compress the content if.")]
        [InlineData("http://localhost/reg", "testAccount", "DummyDUMMYpZxLeDumMyyN52gJj+ZlGE0ipRi9PaTcn9AU4epwvsngE5rLSMk9TwpazxUtzeyBnFeWFAdummyw==", "testContainer", "testStoragePath", "azure")]
        public void AzureFactory(string storageBaseAddress,
                                 string storageAccountName,
                                 string storageKeyValue,
                                 string storageContainer,
                                 string storagePath,
                                 string storageType)
        {
            Dictionary<string, string> arguments = new Dictionary<string, string>()
            {
                { Arguments.StorageBaseAddress, storageBaseAddress },
                { Arguments.StorageAccountName, storageAccountName },
                { Arguments.StorageKeyValue, storageKeyValue },
                { Arguments.StorageContainer, storageContainer },
                { Arguments.StoragePath, storagePath },
                { Arguments.StorageType, storageType}
            };

            StorageFactory factory = CommandHelpers.CreateStorageFactory(arguments, true);
            AzureStorageFactory azureFactory = factory as AzureStorageFactory;
            // Assert
            Assert.True(azureFactory != null, "The CreateCompressedStorageFactory should return an AzureStorageFactory type.");
            Assert.False(azureFactory.CompressContent, "The azure storage factory should not compress the content.");
        }
    }
}
