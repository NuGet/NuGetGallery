// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Ng;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace NgTests
{
    public class CommandHelpersTests
    {
        private const string DummyKey = "DummyDUMMYpZxLeDumMyyN52gJj+ZlGE0ipRi9PaTcn9AU4epwvsngE5rLSMk9TwpazxUtzeyBnFeWFAdummyw==";

        public class TheGetEndpointConfigurationMethod
        {
            [Fact]
            public void ExtractsFlatContainerAndRegistrationCursorUrl()
            {
                // Arrange
                Dictionary<string, string> arguments = new Dictionary<string, string>()
                {
                    { Arguments.FlatContainerCursorUri, "https://example/v3/flatcontainer/cursor.json" },
                    { Arguments.RegistrationCursorUri, "https://example/v3/registration/cursor.json" },
                };

                // Act
                EndpointConfiguration config = CommandHelpers.GetEndpointConfiguration(arguments);

                // Assert
                Assert.Equal("https://example/v3/flatcontainer/cursor.json", config.FlatContainerCursorUri.AbsoluteUri);
                Assert.Equal("https://example/v3/registration/cursor.json", config.RegistrationCursorUri.AbsoluteUri);
            }

            [Fact]
            public void MatchesSearchSettingsBySuffix()
            {
                // Arrange
                Dictionary<string, string> arguments = new Dictionary<string, string>()
                {
                    { Arguments.FlatContainerCursorUri, "https://example/v3/flatcontainer/cursor.json" },
                    { Arguments.RegistrationCursorUri, "https://example/v3/registration/cursor.json" },
                    { Arguments.SearchBaseUriPrefix + "a", "https://example-search-a/" },
                    { Arguments.SearchCursorUriPrefix + "a", "https://example/v3/search-a/cursor.json" },
                    { Arguments.SearchCursorSasValuePrefix + "a", "SIG" },
                    { Arguments.SearchBaseUriPrefix + "b", "https://example-search-b/" },
                    { Arguments.SearchCursorUriPrefix + "b-main", "https://example/v3/search-b-main/cursor.json" },
                    { Arguments.SearchCursorUriPrefix + "b-preview", "https://example/v3/search-b-preview/cursor.json" },
                };

                // Act
                EndpointConfiguration config = CommandHelpers.GetEndpointConfiguration(arguments);

                // Assert
                Assert.Contains("a", config.InstanceNameToSearchConfiguration.Keys);
                var configA = config.InstanceNameToSearchConfiguration["a"];
                Assert.Equal("https://example-search-a/", configA.BaseUri.AbsoluteUri);
                Assert.Single(configA.Cursors);

                Assert.Equal("https://example/v3/search-a/cursor.json", configA.Cursors[0].CursorUri.AbsoluteUri);
                Assert.NotNull(configA.Cursors[0].BlobClient);
                Assert.Equal(SearchCursorCredentialType.AzureSasCredential, configA.Cursors[0].CredentialType);

                Assert.Contains("b", config.InstanceNameToSearchConfiguration.Keys);
                var configB = config.InstanceNameToSearchConfiguration["b"];
                Assert.Equal("https://example-search-b/", configB.BaseUri.AbsoluteUri);
                Assert.Equal(2, configB.Cursors.Count);

                Assert.Equal("https://example/v3/search-b-main/cursor.json", configB.Cursors[0].CursorUri.AbsoluteUri);
                Assert.Null(configB.Cursors[0].BlobClient);
                Assert.Equal(SearchCursorCredentialType.Anonymous, configB.Cursors[0].CredentialType);

                Assert.Equal("https://example/v3/search-b-preview/cursor.json", configB.Cursors[1].CursorUri.AbsoluteUri);
                Assert.Null(configB.Cursors[1].BlobClient);
                Assert.Equal(SearchCursorCredentialType.Anonymous, configB.Cursors[1].CredentialType);

                Assert.Equal(2, config.InstanceNameToSearchConfiguration.Count);

                Assert.Equal("https://example/v3/flatcontainer/cursor.json", config.FlatContainerCursorUri.AbsoluteUri);
                Assert.Equal("https://example/v3/registration/cursor.json", config.RegistrationCursorUri.AbsoluteUri);
            }

            [Fact]
            public void CanUseDefaultAzureCredentialForSearchCursor()
            {
                // Arrange
                Dictionary<string, string> arguments = new Dictionary<string, string>()
                {
                    { Arguments.FlatContainerCursorUri, "https://example/v3/flatcontainer/cursor.json" },
                    { Arguments.RegistrationCursorUri, "https://example/v3/registration/cursor.json" },
                    { Arguments.SearchBaseUriPrefix + "a", "https://example-search-a/" },
                    { Arguments.SearchCursorUriPrefix + "a", "https://example/v3/search-a/cursor.json" },
                    { Arguments.SearchCursorUseManagedIdentityPrefix + "a", "true" },
                };

                // Act
                EndpointConfiguration config = CommandHelpers.GetEndpointConfiguration(arguments);

                // Assert
                Assert.Contains("a", config.InstanceNameToSearchConfiguration.Keys);
                var configA = config.InstanceNameToSearchConfiguration["a"];
                Assert.Equal("https://example-search-a/", configA.BaseUri.AbsoluteUri);
                Assert.Single(configA.Cursors);

                Assert.Equal("https://example/v3/search-a/cursor.json", configA.Cursors[0].CursorUri.AbsoluteUri);
                Assert.NotNull(configA.Cursors[0].BlobClient);
                Assert.Equal(SearchCursorCredentialType.DefaultAzureCredential, configA.Cursors[0].CredentialType);
            }

            [Fact]
            public void CanUseManagedIdentityCredentialForSearchCursor()
            {
                // Arrange
                Dictionary<string, string> arguments = new Dictionary<string, string>()
                {
                    { Arguments.FlatContainerCursorUri, "https://example/v3/flatcontainer/cursor.json" },
                    { Arguments.RegistrationCursorUri, "https://example/v3/registration/cursor.json" },
                    { Arguments.SearchBaseUriPrefix + "a", "https://example-search-a/" },
                    { Arguments.SearchCursorUriPrefix + "a", "https://example/v3/search-a/cursor.json" },
                    { Arguments.SearchCursorUseManagedIdentityPrefix + "a", "true" },
                    { Arguments.ClientId, "my-msi" },
                };

                // Act
                EndpointConfiguration config = CommandHelpers.GetEndpointConfiguration(arguments);

                // Assert
                Assert.Contains("a", config.InstanceNameToSearchConfiguration.Keys);
                var configA = config.InstanceNameToSearchConfiguration["a"];
                Assert.Equal("https://example-search-a/", configA.BaseUri.AbsoluteUri);
                Assert.Single(configA.Cursors);

                Assert.Equal("https://example/v3/search-a/cursor.json", configA.Cursors[0].CursorUri.AbsoluteUri);
                Assert.NotNull(configA.Cursors[0].BlobClient);
                Assert.Equal(SearchCursorCredentialType.ManagedIdentityCredential, configA.Cursors[0].CredentialType);
            }

            [Fact]
            public void PrefersTokenCredentialOverSasValue()
            {
                // Arrange
                Dictionary<string, string> arguments = new Dictionary<string, string>()
                {
                    { Arguments.FlatContainerCursorUri, "https://example/v3/flatcontainer/cursor.json" },
                    { Arguments.RegistrationCursorUri, "https://example/v3/registration/cursor.json" },
                    { Arguments.SearchBaseUriPrefix + "a", "https://example-search-a/" },
                    { Arguments.SearchCursorUriPrefix + "a", "https://example/v3/search-a/cursor.json" },
                    { Arguments.SearchCursorSasValuePrefix + "a", "SIG" },
                    { Arguments.SearchCursorUseManagedIdentityPrefix + "a", "true" },
                };

                // Act
                EndpointConfiguration config = CommandHelpers.GetEndpointConfiguration(arguments);

                // Assert
                Assert.Contains("a", config.InstanceNameToSearchConfiguration.Keys);
                var configA = config.InstanceNameToSearchConfiguration["a"];
                Assert.Equal("https://example-search-a/", configA.BaseUri.AbsoluteUri);
                Assert.Single(configA.Cursors);

                Assert.Equal("https://example/v3/search-a/cursor.json", configA.Cursors[0].CursorUri.AbsoluteUri);
                Assert.NotNull(configA.Cursors[0].BlobClient);
                Assert.Equal(SearchCursorCredentialType.DefaultAzureCredential, configA.Cursors[0].CredentialType);
            }
        }

        public class TheCreateStorageQueueMethod
        {
            [Fact]
            public void Succeeds()
            {
                // Arrange
                Dictionary<string, string> arguments = new Dictionary<string, string>()
                {
                    { Arguments.StorageType, "azure" },
                    { Arguments.StorageAccountName, "testAccount" },
                    { Arguments.StorageKeyValue, DummyKey },
                    { Arguments.StorageQueueName, "myqueue" },
                };

                // Act
                NuGet.Services.Storage.IStorageQueue<string> queue = CommandHelpers.CreateStorageQueue<string>(arguments, version: 1);

                // Assert
                Assert.NotNull(queue);
            }
        }

        public class TheCreateStorageFactoryMethod
        {

            [Fact]
            public void DefaultsToAzurePublicWithNoCompression()
            {
                // Arrange
                Dictionary<string, string> arguments = new Dictionary<string, string>()
                {
                    { Arguments.StorageType, "azure" },
                    { Arguments.StorageBaseAddress, "http://localhost/reg" },
                    { Arguments.StorageAccountName, "testAccount" },
                    { Arguments.StorageKeyValue, DummyKey },
                    { Arguments.StorageContainer, "testContainer" },
                    { Arguments.StoragePath, "testStoragePath" },
                    { Arguments.StorageUseManagedIdentity, "false" },
                };

                // Act
                StorageFactory factory = CommandHelpers.CreateStorageFactory(arguments, true);

                // Assert
                var azureFactory = Assert.IsType<AzureStorageFactory>(factory);
                Assert.False(azureFactory.CompressContent, "The azure storage factory should not compress the content.");
                Assert.Equal("https://testaccount.blob.core.windows.net/testContainer/testStoragePath/", azureFactory.DestinationAddress.AbsoluteUri);
            }

            [Fact]
            public void AllowsCustomStorageSuffix()
            {
                // Arrange
                Dictionary<string, string> arguments = new Dictionary<string, string>()
                {
                    { Arguments.StorageType, "azure" },
                    { Arguments.StorageBaseAddress, "http://localhost/reg" },
                    { Arguments.StorageAccountName, "testAccount" },
                    { Arguments.StorageKeyValue, DummyKey },
                    { Arguments.StorageContainer, "testContainer" },
                    { Arguments.StoragePath, "testStoragePath" },
                    { Arguments.StorageSuffix, "core.chinacloudapi.cn" },
                    { Arguments.StorageUseManagedIdentity, "false" }
                };

                // Act
                StorageFactory factory = CommandHelpers.CreateStorageFactory(arguments, true);

                // Assert
                var azureFactory = Assert.IsType<AzureStorageFactory>(factory);
                Assert.Equal("https://testaccount.blob.core.chinacloudapi.cn/testContainer/testStoragePath/", azureFactory.DestinationAddress.AbsoluteUri);
            }
        }
    }
}
