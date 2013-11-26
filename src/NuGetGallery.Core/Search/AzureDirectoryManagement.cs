﻿using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.IO;
using System.Net;
using System.Threading;

namespace NuGetGallery
{
    public static class AzureDirectoryManagement
    {
        public static TextWriter TraceWriter = Console.Out;

        public static void ForceUnlockAzureDirectory(CloudStorageAccount cloudStorageAccount, string container)
        {
            //  unlocks the write.lock object - this should only be used after a system crash and only form a singleton

            CloudBlobClient cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
            CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(container);
            CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference("write.lock");

            try
            {
                TraceWriter.WriteLine("About to attempt to BreakLease");

                cloudBlockBlob.BreakLease(TimeSpan.FromMilliseconds(1));

                TraceWriter.WriteLine("BreakLease Success");
            }
            catch (StorageException e)
            {
                TraceWriter.WriteLine("BreakLease Exception");

                //  we will get a 409 "Conflict" if the lease is not there - ignore that case as all we were trying to do was drop the lease anyhow

                if (e.InnerException is WebException)
                {
                    HttpWebResponse response = (HttpWebResponse)((WebException)e.InnerException).Response;
                    if (response.StatusCode != HttpStatusCode.Conflict)
                    {
                        throw;
                    }

                    TraceWriter.WriteLine("BreakLease Exception is harmless");
                }
                else
                {
                    throw;
                }
            }
            Thread.Sleep(3000);
        }
    }
}
