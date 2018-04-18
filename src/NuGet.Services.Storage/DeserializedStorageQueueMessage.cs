// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Storage
{
    /// <summary>
    /// Internal class used by <see cref="IMessageSerializer{T}"/> to store a reference to the serialized message returned by the implementor of <see cref="StorageQueue{T}"/>.
    /// </summary>
    internal class DeserializedStorageQueueMessage<T> : StorageQueueMessage<T>
    {
        internal StorageQueueMessage Message { get; }

        public DeserializedStorageQueueMessage(T contents, StorageQueueMessage message)
            : base(contents, message.DequeueCount)
        {
            Message = message;
        }
    }
}