// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Storage;
using Xunit;

namespace CatalogMetadataTests
{
    public class CloudBlobDirectoryWrapperFacts
    {
        public class TheUriProperty
        {
            [Theory]
            [InlineData("", "https://devstoreaccount1.blob.core.windows.net/containerName/")]
            [InlineData("directoryPrefix", "https://devstoreaccount1.blob.core.windows.net/containerName/directoryPrefix")]
            [InlineData("directoryPrefix/foo", "https://devstoreaccount1.blob.core.windows.net/containerName/directoryPrefix/foo")]
            public void ReturnsTheUriWithVariousPrefixes(string directoryPrefix, string expectedUri)
            {
                // Arrange
                var serviceClient = new BlobServiceClientFactory("DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=fake");
                var directory = new CloudBlobDirectoryWrapper(serviceClient, "containerName", directoryPrefix);

                // Act
                var uri = directory.Uri;

                // Assert
                Assert.Equal(new Uri(expectedUri), uri);
            }
        }
    }
}
