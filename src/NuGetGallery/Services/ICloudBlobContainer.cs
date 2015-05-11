// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery
{
    public interface ICloudBlobContainer
    {
        Task CreateIfNotExistAsync();
        Task SetPermissionsAsync(BlobContainerPermissions permissions);
        ISimpleCloudBlob GetBlobReference(string blobAddressUri);
    }
}
