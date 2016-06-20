// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace CatalogTests
{
    public class InstallDataBrowser
    {
        public static void Test0()
        {
            StorageCredentials credentials = new StorageCredentials("", "");

            CloudStorageAccount account = new CloudStorageAccount(credentials, true);

            CloudBlobClient client = account.CreateCloudBlobClient();

            CloudBlobContainer container = client.GetContainerReference("ver36");

            CloudBlockBlob blob = container.GetBlockBlobReference("index.html");

            blob.Properties.ContentType = "text/html";
            blob.Properties.CacheControl = "no-store";

            blob.UploadFromFile("DataBrowser\\index.html");

            Console.WriteLine(blob.Uri);
        }
    }
}