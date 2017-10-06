// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Storage
{
    public class StorageQueueMessage
    {
        public string Contents { get; }

        public StorageQueueMessage(string contents)
        {
            Contents = contents;
        }
    }

    public class StorageQueueMessage<T>
    {
        public T Contents { get; }

        public StorageQueueMessage(T contents)
        {
            Contents = contents;
        }
    }
}