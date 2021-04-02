// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;

namespace NuGetGallery
{
    public static class SimpleCloudBlobExtensions
    {
        /// <summary>
        /// Retrieves the blob contents as a string assuming UTF8 encoding if the blob exists.
        /// </summary>
        /// <param name="blob">Blob reference.</param>
        /// <returns>The text content of the blob or null if the blob does not exist.</returns>
        public static async Task<string> DownloadTextIfExistAsync(this ISimpleCloudBlob blob)
        {
            using (var stream = new MemoryStream())
            {
                try
                {
                    await blob.DownloadToStreamAsync(stream);
                }
                catch (StorageException e) when (IsNotFoundException(e))
                {
                    return null;
                }

                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        /// <summary>
        /// Calls <see cref="ISimpleCloudBlob.FetchAttributesAsync()"/> and determines if blob exists.
        /// </summary>
        /// <param name="blob">Blob reference</param>
        /// <returns>True if <see cref="ISimpleCloudBlob.FetchAttributesAsync()"/> call succeeded, false if blob does not exist.</returns>
        public static async Task<bool> FetchAttributesIfExistsAsync(this ISimpleCloudBlob blob)
        {
            try
            {
                await blob.FetchAttributesAsync();
            }
            catch (StorageException e) when (IsNotFoundException(e))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Calls <see cref="ISimpleCloudBlob.OpenReadAsync(AccessCondition)"/> without access condition and returns
        /// resulting stream if blob exists.
        /// </summary>
        /// <param name="blob">Blob reference.</param>
        /// <returns>Stream if the call was successful, false if blob does not exist.</returns>
        public static async Task<Stream> OpenReadIfExistAsync(this ISimpleCloudBlob blob)
        {
            try
            {
                return await blob.OpenReadAsync(accessCondition: null);
            }
            catch (StorageException e) when (IsNotFoundException(e))
            {
                return null;
            }
        }

        private static bool IsNotFoundException(StorageException e)
            => ((e.InnerException as WebException)?.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound;
    }
}
