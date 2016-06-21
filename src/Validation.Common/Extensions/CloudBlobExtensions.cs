// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGet.Jobs.Validation.Common.Extensions
{
    public static class CloudBlobExtensions
    {
        public static async Task<string> TryAcquireLeaseAsync(this ICloudBlob blob, TimeSpan leaseTime, CancellationToken cancellationToken)
        {
            string leaseId;
            try
            {
                var sourceBlobExists = await blob.ExistsAsync(cancellationToken);
                if (!sourceBlobExists)
                {
                    return null;
                }
                
                leaseId = await blob.AcquireLeaseAsync(leaseTime, null, cancellationToken);
            }
            catch (StorageException storageException)
            {
                // check if this is a 409 Conflict with a StatusDescription stating that "There is already a lease present."
                // or 404 NotFound (might have been removed by another other instance of this job)
                var webException = storageException.InnerException as WebException;
                var httpWebResponse = webException?.Response as HttpWebResponse;
                if (httpWebResponse != null)
                {
                    if ((httpWebResponse.StatusCode == HttpStatusCode.Conflict
                         && httpWebResponse.StatusDescription == "There is already a lease present.") || httpWebResponse.StatusCode == HttpStatusCode.NotFound)
                    {
                        return null;
                    }
                }

                throw;
            }

            return leaseId;
        }
    }
}