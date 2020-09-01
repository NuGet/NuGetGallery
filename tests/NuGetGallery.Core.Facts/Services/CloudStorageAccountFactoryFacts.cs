// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.Services
{
    public class CloudStorageAccountFactoryFacts
    {
        [Fact]
        public void CreateCloudStorageAccountWithSASToken()
        {
            var sasToken = "?st=2018-03-12T14%3A55%3A00Z&se=2018-03-13T14%3A55%3A00Z&sp=r&sv=2017-04-17&sr=c&sig=dCXxOlBp6dQHqxTeCRABpr1lfpt40QUaHsAQqs9zHds%3D";
            var accountName = "test";

            var cloudStorageAccount = CloudStorageAccountFactory.CreateCloudStorageAccount(sasToken, accountName);

            Assert.Equal(sasToken, cloudStorageAccount.Credentials.SASToken);
            Assert.True(cloudStorageAccount.Credentials.IsSAS);
            Assert.Equal($"https://{accountName}.blob.core.windows.net/", cloudStorageAccount.BlobEndpoint.AbsoluteUri);
        }

        [Fact]
        public void CreateCloudStorageAccountWithAccountKey()
        {
            var accountName = "test";
            var storageConnectionString = $"DefaultEndpointsProtocol=https;AccountName={accountName};AccountKey=;EndpointSuffix=core.windows.net";

            var cloudStorageAccount = CloudStorageAccountFactory.CreateCloudStorageAccount(storageConnectionString);

            Assert.Equal(accountName, cloudStorageAccount.Credentials.AccountName);
            Assert.True(cloudStorageAccount.Credentials.IsSharedKey);
            Assert.Equal($"https://{accountName}.blob.core.windows.net/", cloudStorageAccount.BlobEndpoint.AbsoluteUri);
        }
    }
}
