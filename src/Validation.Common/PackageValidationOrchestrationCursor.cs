// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;

namespace NuGet.Jobs.Validation.Common
{
    public class PackageValidationOrchestrationCursor
    {
        private readonly CloudBlockBlob _cursorBlob;

        public PackageValidationOrchestrationCursor(CloudStorageAccount cloudStorageAccount, string containerName, string cursorName)
        {
            var cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
            var container = cloudBlobClient.GetContainerReference(containerName);
            container.CreateIfNotExists();

            _cursorBlob = container.GetBlockBlobReference(cursorName);
        }

        public DateTimeOffset? LastCreated { get; set; }
        public DateTimeOffset? LastEdited { get; set; }

        public async Task LoadAsync()
        {
            Trace.TraceInformation("Start loading cursor from {0}...", _cursorBlob.Uri);

            if (await _cursorBlob.ExistsAsync())
            {
                var json = await _cursorBlob.DownloadTextAsync();

                var cursorObject = JObject.Parse(json);
                LastCreated = cursorObject["lastCreated"].ToObject<DateTimeOffset?>();
                LastEdited = cursorObject["lastEdited"].ToObject<DateTimeOffset?>();

                Trace.TraceInformation("Cursor value: {0}", json);
            }

            Trace.TraceInformation("Finished loading cursor from {0}.", _cursorBlob.Uri);
        }

        public async Task SaveAsync()
        {
            Trace.TraceInformation("Start saving cursor to {0}...", _cursorBlob.Uri);

            var cursorObject = new JObject
            {
                { "lastCreated", LastCreated?.ToString("O") },
                { "lastEdited", LastEdited?.ToString("O") },
            };

            var json = cursorObject.ToString();

            await _cursorBlob.UploadTextAsync(json);

            _cursorBlob.Properties.ContentType = "application/json";
            await _cursorBlob.SetPropertiesAsync();

            Trace.TraceInformation("Cursor value: {0}", json);
            Trace.TraceInformation("Finished saving cursor to {0}.", _cursorBlob.Uri);
        }
    }
}