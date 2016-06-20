// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGet.Jobs.Validation.Common
{
    public class NotificationService
        : INotificationService
    {
        private readonly CloudBlobContainer _notificationContainer;

        public NotificationService(CloudStorageAccount cloudStorageAccount, string containerNamePrefix)
        {
            var cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
            _notificationContainer = cloudBlobClient.GetContainerReference(containerNamePrefix + "-notification");
            _notificationContainer.CreateIfNotExists();
        }

        public async Task SendNotificationAsync(string subject, string body)
        {
            await SendNotificationAsync(null, subject, body);
        }

        public async Task SendNotificationAsync(string category, string subject, string body)
        {
            var fileName = $"{DateTime.UtcNow.ToString("O")}.txt";
            if (!string.IsNullOrEmpty(category))
            {
                fileName = $"{category}/{fileName}";
            }

            var notificationBlob = _notificationContainer.GetBlockBlobReference(fileName);

            await notificationBlob.UploadTextAsync($"{subject}\r\n\r\n{body}");
        }
    }
}