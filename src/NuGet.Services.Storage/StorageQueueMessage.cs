// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Storage
{
    public class StorageQueueMessage
    {
        public string Contents { get; }
        public int DequeueCount { get; }

        public StorageQueueMessage(string contents, int dequeueCount)
        {
            Contents = contents;
            DequeueCount = dequeueCount;
        }
    }

    public class StorageQueueMessage<T>
    {
        public T Contents { get; }
        public int DequeueCount { get; }

        public StorageQueueMessage(T contents, int dequeueCount)
        {
            Contents = contents;
            DequeueCount = dequeueCount;
        }
    }
}