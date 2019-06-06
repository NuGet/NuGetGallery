// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Ng;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace NgTests
{
    public class StorageFactoryTests
    {
        [Theory]
        [Description("The azure compressed factory should compress the content.")]
        [InlineData("http://localhost/reg", "testAccount", "DummyDUMMYpZxLeDumMyyN52gJj+ZlGE0ipRi9PaTcn9AU4epwvsngE5rLSMk9TwpazxUtzeyBnFeWFAdummyw==", "testContainer", "testStoragePath", "azure")]
        public void AzureCompressedFactory(string storageBaseAddress,
                                            string storageAccountName,
                                            string storageKeyValue,
                                            string storageContainer,
                                            string storagePath,
                                            string storageType)
        {
            Dictionary<string, string> arguments = new Dictionary<string, string>()
            {
                { Arguments.CompressedStorageBaseAddress, storageBaseAddress },
                { Arguments.CompressedStorageAccountName, storageAccountName },
                { Arguments.CompressedStorageKeyValue, storageKeyValue },
                { Arguments.CompressedStorageContainer, storageContainer },
                { Arguments.CompressedStoragePath, storagePath },
                { Arguments.StorageType, storageType},
                { Arguments.UseCompressedStorage, "true"}
            };

            StorageFactory factory =  CommandHelpers.CreateCompressedStorageFactory(arguments, true);
            AzureStorageFactory azureFactory = factory as AzureStorageFactory;
            // Assert
            Assert.True(azureFactory!=null, "The CreateCompressedStorageFactory should return an AzureStorageFactory.");
            Assert.True(azureFactory.CompressContent, "The compressed storage factory should compress the content.");
        }

        [Theory]
        [Description("The azure compressed factory will be null if the UseCompressedStorage is false.")]
        [InlineData("http://localhost/reg", "testAccount", "DummyDUMMYpZxLeDumMyyN52gJj+ZlGE0ipRi9PaTcn9AU4epwvsngE5rLSMk9TwpazxUtzeyBnFeWFAdummyw==", "testContainer", "testStoragePath", "azure")]
        public void AzureCompressedFactoryNull(string storageBaseAddress,
                                            string storageAccountName,
                                            string storageKeyValue,
                                            string storageContainer,
                                            string storagePath,
                                            string storageType)
        {
            Dictionary<string, string> arguments = new Dictionary<string, string>()
            {
                { Arguments.CompressedStorageBaseAddress, storageBaseAddress },
                { Arguments.CompressedStorageAccountName, storageAccountName },
                { Arguments.CompressedStorageKeyValue, storageKeyValue },
                { Arguments.CompressedStorageContainer, storageContainer },
                { Arguments.CompressedStoragePath, storagePath },
                { Arguments.StorageType, storageType },
                { Arguments.UseCompressedStorage, "false" }
            };

            StorageFactory factory = CommandHelpers.CreateCompressedStorageFactory(arguments, true);
            // Assert
            Assert.True(factory == null, "The CompressedStorageFactory should be null when the UseCompressedStorage is false.");
        }

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

        [Theory]
        [Description("The SemVer 2.0.0 Azure factory should compress the content.")]
        [InlineData("http://localhost/reg", "testAccount", "DummyDUMMYpZxLeDumMyyN52gJj+ZlGE0ipRi9PaTcn9AU4epwvsngE5rLSMk9TwpazxUtzeyBnFeWFAdummyw==", "testContainer", "testStoragePath")]
        public void AzureSemVer2Factory(string storageBaseAddress,
                                        string storageAccountName,
                                        string storageKeyValue,
                                        string storageContainer,
                                        string storagePath)
        {
            Dictionary<string, string> arguments = new Dictionary<string, string>()
            {
                { Arguments.SemVer2StorageBaseAddress, storageBaseAddress },
                { Arguments.SemVer2StorageAccountName, storageAccountName },
                { Arguments.SemVer2StorageKeyValue, storageKeyValue },
                { Arguments.SemVer2StorageContainer, storageContainer },
                { Arguments.SemVer2StoragePath, storagePath },
                { Arguments.StorageType, "azure" },
                { Arguments.UseSemVer2Storage, "true" }
            };

            var factory = CommandHelpers.CreateSemVer2StorageFactory(arguments, verbose: true);

            // Assert
            var azureFactory = Assert.IsType<AzureStorageFactory>(factory);
            Assert.True(azureFactory.CompressContent, "The Azure storage factory should compress the content.");
        }

        [Fact]
        public void CreateRegistrationStorageFactories_WithAllThreeFactories()
        {
            // Arrange
            var arguments = new Dictionary<string, string>()
            {
                { Arguments.StorageType, "azure" },

                { Arguments.StorageBaseAddress, "http://localhost/reg" },
                { Arguments.StorageAccountName, "testAccount" },
                { Arguments.StorageKeyValue, "ADummyDUMMYpZxLeDumMyyN52gJj+ZlGE0ipRi9PaTcn9A4epwvsngE5rLSMk9TwpazxUtzeyBnFeWFAdummyw==" },
                { Arguments.StorageContainer, "testContainer" },
                { Arguments.StoragePath, "testStoragePath" },

                { Arguments.UseCompressedStorage, "true" },
                { Arguments.CompressedStorageBaseAddress, "http://localhost/reg-gz" },
                { Arguments.CompressedStorageAccountName, "testAccount-gz" },
                { Arguments.CompressedStorageKeyValue, "BDummyDUMMYpZxLeDumMyyN52gJj+ZlGE0ipRi9PaTcn9A4epwvsngE5rLSMk9TwpazxUtzeyBnFeWFAdummyw==" },
                { Arguments.CompressedStorageContainer, "testContainer-gz" },
                { Arguments.CompressedStoragePath, "testStoragePath-gz" },

                { Arguments.UseSemVer2Storage, "true" },
                { Arguments.SemVer2StorageBaseAddress, "http://localhost/reg-gz-semver2" },
                { Arguments.SemVer2StorageAccountName, "testAccount-semver2" },
                { Arguments.SemVer2StorageKeyValue, "CDummyDUMMYpZxLeDumMyyN52gJj+ZlGE0ipRi9PaTcn9A4epwvsngE5rLSMk9TwpazxUtzeyBnFeWFAdummyw==" },
                { Arguments.SemVer2StorageContainer, "testContainer-semver2" },
                { Arguments.SemVer2StoragePath, "testStoragePath-semver2" },
            };

            // Act
            var factories = CommandHelpers.CreateRegistrationStorageFactories(arguments, verbose: true);

            // Assert
            var legacy = Assert.IsType<AggregateStorageFactory>(factories.LegacyStorageFactory);
            Assert.True(legacy.Verbose, "verbose should be true on the aggregate storage factory");
            Assert.Equal(legacy.BaseAddress, new Uri("http://localhost/reg/testStoragePath/"));

            var originalFactory = Assert.IsType<AzureStorageFactory>(legacy.PrimaryStorageFactory);
            Assert.True(originalFactory.Verbose, "verbose should be true on the original storage factory");
            Assert.False(originalFactory.CompressContent, "compress should be false on the original storage factory");
            Assert.Equal(originalFactory.BaseAddress, new Uri("http://localhost/reg/testStoragePath/"));

            Assert.Single(legacy.SecondaryStorageFactories);
            var compressFactory = Assert.IsType<AzureStorageFactory>(legacy.SecondaryStorageFactories.First());
            Assert.True(compressFactory.Verbose, "verbose should be true on the compress storage factory");
            Assert.True(compressFactory.CompressContent, "compress should be true on the compress storage factory");
            Assert.Equal(compressFactory.BaseAddress, new Uri("http://localhost/reg-gz/testStoragePath-gz/"));

            var semVer2 = Assert.IsType<AzureStorageFactory>(factories.SemVer2StorageFactory);
            Assert.True(semVer2.Verbose, "verbose should be true on the SemVer 2.0.0 storage factory");
            Assert.True(semVer2.CompressContent, "compress should be true on the SemVer 2.0.0 storage factory");
            Assert.Equal(semVer2.BaseAddress, new Uri("http://localhost/reg-gz-semver2/testStoragePath-semver2/"));
        }

        [Fact]
        public void CreateRegistrationStorageFactories_WithNoCompressFactory()
        {
            // Arrange
            var arguments = new Dictionary<string, string>()
            {
                { Arguments.StorageType, "azure" },

                { Arguments.StorageBaseAddress, "http://localhost/reg" },
                { Arguments.StorageAccountName, "testAccount" },
                { Arguments.StorageKeyValue, "ADummyDUMMYpZxLeDumMyyN52gJj+ZlGE0ipRi9PaTcn9A4epwvsngE5rLSMk9TwpazxUtzeyBnFeWFAdummyw==" },
                { Arguments.StorageContainer, "testContainer" },
                { Arguments.StoragePath, "testStoragePath" },

                { Arguments.UseCompressedStorage, "false" },

                { Arguments.UseSemVer2Storage, "true" },
                { Arguments.SemVer2StorageBaseAddress, "http://localhost/reg-gz-semver2" },
                { Arguments.SemVer2StorageAccountName, "testAccount-semver2" },
                { Arguments.SemVer2StorageKeyValue, "CDummyDUMMYpZxLeDumMyyN52gJj+ZlGE0ipRi9PaTcn9A4epwvsngE5rLSMk9TwpazxUtzeyBnFeWFAdummyw==" },
                { Arguments.SemVer2StorageContainer, "testContainer-semver2" },
                { Arguments.SemVer2StoragePath, "testStoragePath-semver2" },
            };

            // Act
            var factories = CommandHelpers.CreateRegistrationStorageFactories(arguments, verbose: true);

            // Assert
            var originalFactory = Assert.IsType<AzureStorageFactory>(factories.LegacyStorageFactory);
            Assert.True(originalFactory.Verbose, "verbose should be true on the original storage factory");
            Assert.False(originalFactory.CompressContent, "compress should be false on the original storage factory");
            Assert.Equal(originalFactory.BaseAddress, new Uri("http://localhost/reg/testStoragePath/"));

            var semVer2 = Assert.IsType<AzureStorageFactory>(factories.SemVer2StorageFactory);
            Assert.True(semVer2.Verbose, "verbose should be true on the SemVer 2.0.0 storage factory");
            Assert.True(semVer2.CompressContent, "compress should be true on the SemVer 2.0.0 storage factory");
            Assert.Equal(semVer2.BaseAddress, new Uri("http://localhost/reg-gz-semver2/testStoragePath-semver2/"));
        }

        [Fact]
        public void CreateRegistrationStorageFactories_WithNoSemVer2Factory()
        {
            // Arrange
            var arguments = new Dictionary<string, string>()
            {
                { Arguments.StorageType, "azure" },

                { Arguments.StorageBaseAddress, "http://localhost/reg" },
                { Arguments.StorageAccountName, "testAccount" },
                { Arguments.StorageKeyValue, "ADummyDUMMYpZxLeDumMyyN52gJj+ZlGE0ipRi9PaTcn9A4epwvsngE5rLSMk9TwpazxUtzeyBnFeWFAdummyw==" },
                { Arguments.StorageContainer, "testContainer" },
                { Arguments.StoragePath, "testStoragePath" },

                { Arguments.UseCompressedStorage, "true" },
                { Arguments.CompressedStorageBaseAddress, "http://localhost/reg-gz" },
                { Arguments.CompressedStorageAccountName, "testAccount-gz" },
                { Arguments.CompressedStorageKeyValue, "BDummyDUMMYpZxLeDumMyyN52gJj+ZlGE0ipRi9PaTcn9A4epwvsngE5rLSMk9TwpazxUtzeyBnFeWFAdummyw==" },
                { Arguments.CompressedStorageContainer, "testContainer-gz" },
                { Arguments.CompressedStoragePath, "testStoragePath-gz" },

                { Arguments.UseSemVer2Storage, "false" },
            };

            // Act
            var factories = CommandHelpers.CreateRegistrationStorageFactories(arguments, verbose: true);

            // Assert
            var legacy = Assert.IsType<AggregateStorageFactory>(factories.LegacyStorageFactory);
            Assert.True(legacy.Verbose, "verbose should be true on the aggregate storage factory");
            Assert.Equal(legacy.BaseAddress, new Uri("http://localhost/reg/testStoragePath/"));

            var originalFactory = Assert.IsType<AzureStorageFactory>(legacy.PrimaryStorageFactory);
            Assert.True(originalFactory.Verbose, "verbose should be true on the original storage factory");
            Assert.False(originalFactory.CompressContent, "compress should be false on the original storage factory");
            Assert.Equal(originalFactory.BaseAddress, new Uri("http://localhost/reg/testStoragePath/"));

            Assert.Single(legacy.SecondaryStorageFactories);
            var compressFactory = Assert.IsType<AzureStorageFactory>(legacy.SecondaryStorageFactories.First());
            Assert.True(compressFactory.Verbose, "verbose should be true on the compress storage factory");
            Assert.True(compressFactory.CompressContent, "compress should be true on the compress storage factory");
            Assert.Equal(compressFactory.BaseAddress, new Uri("http://localhost/reg-gz/testStoragePath-gz/"));

            Assert.Null(factories.SemVer2StorageFactory);
        }
    }
}
