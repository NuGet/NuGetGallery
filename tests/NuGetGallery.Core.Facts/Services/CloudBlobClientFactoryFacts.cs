// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.Services
{
    public class CloudBlobClientFactoryFacts
    {
        [Fact]
        public void CreateCloudBlobClientWithSASToken()
        {
            var sasToken = "?st=2018-03-12T14%3A55%3A00Z&se=2018-03-13T14%3A55%3A00Z&sp=r&sv=2017-04-17&sr=c&sig=dCXxOlBp6dQHqxTeCRABpr1lfpt40QUaHsAQqs9zHds%3D";
            var accountName = "test";

            var cloudBlobClient = CloudBlobClientFactory.CreateCloudBlobClient(sasToken, accountName);
            var credentials = cloudBlobClient.Credentials;

            Assert.Equal(sasToken, credentials.SASToken);
            Assert.True(credentials.IsSAS);
            Assert.Equal($"https://{accountName}.blob.core.windows.net/", cloudBlobClient.BaseUri.AbsoluteUri);
        }

        [Fact]
        public void CreateCloudBlobClientWithAccountKey()
        {
            var accountName = "test";
            var storageConnectionString = $"DefaultEndpointsProtocol=https;AccountName={accountName};AccountKey=;EndpointSuffix=core.windows.net";

            var cloudBlobClient = CloudBlobClientFactory.CreateCloudBlobClient(storageConnectionString);
            var credentials = cloudBlobClient.Credentials;

            Assert.Equal(accountName, credentials.AccountName);
            Assert.True(credentials.IsSharedKey);
            Assert.Equal($"https://{accountName}.blob.core.windows.net/", cloudBlobClient.BaseUri.AbsoluteUri);
        }
    }
}
