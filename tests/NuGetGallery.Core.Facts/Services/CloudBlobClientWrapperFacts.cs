// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Xunit;

namespace NuGetGallery.Services
{
    public class CloudBlobClientWrapperFacts
    {
        public class GetBlobFromUri
        {
            [Fact]
            public void UsesQueryStringAsSasToken()
            {
                var blobUrl = "https://example.blob.core.windows.net/packages/nuget.versioning.4.6.0.nupkg";
                var sasToken = "?st=2018-03-12T14%3A55%3A00Z&se=2018-03-13T14%3A55%3A00Z&sp=r&sv=2017-04-17&sr=c&sig=dCXxOlBp6dQHqxTeCRABpr1lfpt40QUaHsAQqs9zHds%3D";
                var uri = new Uri(blobUrl + sasToken);
                var target = new CloudBlobClientWrapper("UseDevelopmentStorage=true", readAccessGeoRedundant: false);

                var blob = target.GetBlobFromUri(uri);

                var innerBlob = Assert.IsType<CloudBlockBlob>(blob
                    .GetType()
                    .GetField("_blob", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(blob));
                Assert.Equal(AuthenticationScheme.SharedKey, innerBlob.ServiceClient.AuthenticationScheme);
                Assert.False(innerBlob.ServiceClient.Credentials.IsAnonymous);
                Assert.True(innerBlob.ServiceClient.Credentials.IsSAS);
                Assert.False(innerBlob.ServiceClient.Credentials.IsSharedKey);
                Assert.Equal(sasToken, innerBlob.ServiceClient.Credentials.SASToken);
                Assert.Equal(blobUrl, innerBlob.Uri.AbsoluteUri);
            }

            [Fact]
            public void UsesAnonymousAuthWhenThereIsNotQueryString()
            {
                var blobUrl = "https://example.blob.core.windows.net/packages/nuget.versioning.4.6.0.nupkg";
                var uri = new Uri(blobUrl);
                var target = new CloudBlobClientWrapper("UseDevelopmentStorage=true", readAccessGeoRedundant: false);

                var blob = target.GetBlobFromUri(uri);

                var innerBlob = Assert.IsType<CloudBlockBlob>(blob
                    .GetType()
                    .GetField("_blob", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(blob));
                Assert.Equal(AuthenticationScheme.SharedKey, innerBlob.ServiceClient.AuthenticationScheme);
                Assert.True(innerBlob.ServiceClient.Credentials.IsAnonymous);
                Assert.False(innerBlob.ServiceClient.Credentials.IsSAS);
                Assert.False(innerBlob.ServiceClient.Credentials.IsSharedKey);
                Assert.Equal(blobUrl, innerBlob.Uri.AbsoluteUri);
            }
        }
    }
}
