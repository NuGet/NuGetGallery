// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;
using NuGet.Services.Metadata.Catalog.Persistence;
using Azure.Storage.Blobs;

namespace CatalogMetadataTests
{
    public class CloudBlobDirectoryWrapperFacts
    {
        public class TheUriProperty
        {
            [Theory]
            [InlineData("", "https://test/containerName/")]
            [InlineData("directoryPrefix", "https://test/containerName/directoryPrefix")]
            [InlineData("directoryPrefix/foo", "https://test/containerName/directoryPrefix/foo")]
            public void ReturnsTheUriWithVariousPrefixes(string directoryPrefix, string expectedUri)
            {
                // Arrange
                var serviceClient = new BlobServiceClient(new Uri("https://test"));
                var directory = new CloudBlobDirectoryWrapper(serviceClient, "containerName", directoryPrefix);

                // Act
                var uri = directory.Uri;

                // Assert
                Assert.Equal(new Uri(expectedUri), uri);
            }
        }
    }
}
