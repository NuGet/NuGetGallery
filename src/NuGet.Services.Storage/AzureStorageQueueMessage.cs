// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage.Queue;

namespace NuGet.Services.Storage
{
    internal class AzureStorageQueueMessage : StorageQueueMessage
    {
        internal CloudQueueMessage Message { get; }

        internal AzureStorageQueueMessage(CloudQueueMessage message)
            : base(message.AsString, message.DequeueCount)
        {
            Message = message;
        }

        internal AzureStorageQueueMessage(string contents, int dequeueCount)
            : base(contents, dequeueCount)
        {
            Message = new CloudQueueMessage(contents);
        }
    }
}