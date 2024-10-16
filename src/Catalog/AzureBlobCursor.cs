// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Metadata.Catalog
{
    public class AzureBlobCursor : ReadCursor
    {
        private readonly BlobClient _blobClient;

        public AzureBlobCursor(BlobClient blobClient)
        {
            _blobClient = blobClient ?? throw new ArgumentNullException(nameof(blobClient));
        }

        public override async Task LoadAsync(CancellationToken cancellationToken)
        {
            BlobDownloadResult downloadResult;
            try
            {
                downloadResult = await _blobClient.DownloadContentAsync(cancellationToken);
            }
            catch (RequestFailedException ex)
            {
                Trace.TraceError("AzureBlobCursor.LoadAsync: error {0} {1}", ex.Status, _blobClient.Uri.AbsoluteUri);
                throw;
            }

            var json = downloadResult.Content.ToString();

            JObject obj = JObject.Parse(json);
            Value = obj["value"].ToObject<DateTime>();

            Trace.TraceInformation("AzureBlobCursor.LoadAsync: {0:O} {1}", Value, _blobClient.Uri.AbsoluteUri);
        }
    }
}
