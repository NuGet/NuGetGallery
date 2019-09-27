// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Storage
{
    public class StorageQueue<T> : IStorageQueue<T>
    {
        private IStorageQueue _queue;
        private IMessageSerializer<T> _messageSerializer;
        
        public StorageQueue(IStorageQueue queue, IMessageSerializer<T> contentsSerializer, int version)
            : this(queue, 
                  new TypedMessageSerializer<T>(
                      contentsSerializer, 
                      new JsonMessageSerializer<TypedMessage>(), 
                      version))
        {
        }

        public StorageQueue(IStorageQueue queue, TypedMessageSerializer<T> typedMessageSerializer)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _messageSerializer = typedMessageSerializer ?? throw new ArgumentNullException(nameof(typedMessageSerializer));
        }

        public Task AddAsync(T contents, CancellationToken token)
        {
            return _queue.AddAsync(_messageSerializer.Serialize(contents), token);
        }

        public async Task<StorageQueueMessage<T>> GetNextAsync(CancellationToken token)
        {
            return DeserializeMessage(await _queue.GetNextAsync(token));
        }

        public Task RemoveAsync(StorageQueueMessage<T> message, CancellationToken token)
        {
            return _queue.RemoveAsync(SerializeMessage(message), token);
        }

        public Task<int?> GetMessageCount(CancellationToken token)
        {
            return _queue.GetMessageCount(token);
        }

        private StorageQueueMessage SerializeMessage(StorageQueueMessage<T> message)
        {
            if (message is DeserializedStorageQueueMessage<T> deserializedMessage)
            {
                return deserializedMessage.Message;
            }
            else
            {
                return new StorageQueueMessage(_messageSerializer.Serialize(message.Contents), message.DequeueCount);
            }
        }

        private StorageQueueMessage<T> DeserializeMessage(StorageQueueMessage message)
        {
            if (message == null)
            {
                return null;
            }

            return new DeserializedStorageQueueMessage<T>(_messageSerializer.Deserialize(message.Contents), message);
        }
    }
}