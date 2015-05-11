// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.IO;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery.Operations
{
    public static class CloudBlobExtensions
    {
        public static bool Exists(this ICloudBlob blob)
        {
            try
            {
                blob.FetchAttributes();
                return true;
            }
            catch (StorageException e)
            {
                if (e.RequestInformation.HttpStatusCode == 404)
                    return false;
                
                throw;
            }
        }

        public static void DownloadToFile(this ICloudBlob self, string fileName)
        {
            using (Stream strm = File.OpenWrite(fileName))
            {
                self.DownloadToStream(strm);
            }
        }

        public static void UploadFile(this ICloudBlob self, string fileName)
        {
            using (Stream strm = File.OpenRead(fileName))
            {
                self.UploadFromStream(strm);
            }
        }
    }
}
