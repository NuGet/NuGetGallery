// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

//using Azure.Storage.Blobs;

//namespace NuGet.Services.Metadata.Catalog.Persistence
//{
//    public class CloudBlockBlobClientWrapper : ICloudBlockBlobClient
//    {
//        private readonly BlobServiceClient _blobServiceClient;
//        private BlobClientOptions _defaultClientOptions;

//        public BlobClientOptions DefaultClientOptions
//        {
//            get => _defaultClientOptions;
//        }

//        public CloudBlockBlobClientWrapper(BlobServiceClient blobServiceClient)
//        {
//            _blobServiceClient = blobServiceClient;
//        }

//        public CloudBlockBlobClientWrapper(BlobServiceClient blobServiceClient, BlobClientOptions blobClientOptions)
//            : this(blobServiceClient)
//        {
//            _defaultClientOptions = blobClientOptions;
//        }

//        //// Example method to get a BlobContainerClient
//        //public BlobContainerClient GetBlobContainerClient(string containerName)
//        //{
//        //    return _blobServiceClient.GetBlobContainerClient(containerName);
//        //}

//        //// Example method to get a BlobClient
//        //public BlobClient GetBlobClient(string containerName, string blobName)
//        //{
//        //    var containerClient = GetBlobContainerClient(containerName);
//        //    return containerClient.GetBlobClient(blobName);
//        //}
//    }
//}
