// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Storage.Queues.Models;

namespace NuGet.Services.Storage
{
    internal class AzureStorageQueueMessage : StorageQueueMessage
    {
        public string MessageId { get; set; }
        public string PopReceipt { get; set; }

        internal QueueMessage Message { get; }

        internal AzureStorageQueueMessage(QueueMessage message)
            : base(message.Body.ToString(), message.DequeueCount)
        {
            Message = message;
            MessageId = message.MessageId;
            PopReceipt = message.PopReceipt;
        }

        internal AzureStorageQueueMessage(string contents, int dequeueCount)
            : base(contents, dequeueCount)
        {
            Message = null;
        }
    }
}