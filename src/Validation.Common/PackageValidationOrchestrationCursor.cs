// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;

namespace NuGet.Jobs.Validation.Common
{
    public class PackageValidationOrchestrationCursor
    {
        private readonly CloudBlockBlob _cursorBlob;
        private readonly ILogger<PackageValidationOrchestrationCursor> _logger;

        public PackageValidationOrchestrationCursor(CloudStorageAccount cloudStorageAccount, string containerName, string cursorName, ILoggerFactory loggerFactory)
        {
            var cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
            var container = cloudBlobClient.GetContainerReference(containerName);
            container.CreateIfNotExists();

            _cursorBlob = container.GetBlockBlobReference(cursorName);
            _logger = loggerFactory.CreateLogger<PackageValidationOrchestrationCursor>();
        }

        public DateTimeOffset? LastCreated { get; set; }
        public DateTimeOffset? LastEdited { get; set; }

        public async Task LoadAsync()
        {
            _logger.LogInformation($"Start loading cursor from {{{TraceConstant.Url}}}...", _cursorBlob.Uri);

            if (await _cursorBlob.ExistsAsync())
            {
                var json = await _cursorBlob.DownloadTextAsync();

                var cursorObject = JObject.Parse(json);
                LastCreated = cursorObject["lastCreated"].ToObject<DateTimeOffset?>();
                LastEdited = cursorObject["lastEdited"].ToObject<DateTimeOffset?>();

                _logger.LogInformation($"Cursor value: {{{TraceConstant.CursorValue}}}", json);
            }

            _logger.LogInformation($"Finished loading cursor from {{{TraceConstant.Url}}}.", _cursorBlob.Uri);
        }

        public async Task SaveAsync()
        {
            _logger.LogInformation($"Start saving cursor to {{{TraceConstant.Url}}}...", _cursorBlob.Uri);

            var cursorObject = new JObject
            {
                { "lastCreated", LastCreated?.ToString("O") },
                { "lastEdited", LastEdited?.ToString("O") },
            };

            var json = cursorObject.ToString();

            await _cursorBlob.UploadTextAsync(json);

            _cursorBlob.Properties.ContentType = "application/json";
            await _cursorBlob.SetPropertiesAsync();

            _logger.LogInformation($"Cursor value: {{{TraceConstant.CursorValue}}}", json);
            _logger.LogInformation($"Finished saving cursor to {{{TraceConstant.Url}}}.", _cursorBlob.Uri);
        }
    }
}