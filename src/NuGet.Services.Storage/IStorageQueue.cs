// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Storage
{
    /// <summary>
    /// Represents a queue to add <see cref="StorageQueueMessage"/>s to and receive <see cref="StorageQueueMessage"/>s from.
    /// </summary>
    public interface IStorageQueue
    {
        /// <summary>
        /// Adds a <see cref="StorageQueueMessage"/> to the queue.
        /// </summary>
        /// <param name="message">The message to add.</param>
        /// <param name="token">A token to cancel the task with.</param>
        Task AddAsync(string contents, CancellationToken token);

        /// <summary>
        /// Receives a <see cref="StorageQueueMessage"/> from the queue.
        /// </summary>
        /// <param name="token">A token to cancel the task with.</param>
        /// <returns>A message from the queue.</returns>
        Task<StorageQueueMessage> GetNextAsync(CancellationToken token);

        /// <summary>
        /// Removes a <see cref="StorageQueueMessage"/> from the queue.
        /// </summary>
        /// <param name="message">The message to remove from the queue.</param>
        /// <param name="token">A token to cancel the task with.</param>
        /// <remarks>
        /// This method should throw if <paramref name="message"/> was not returned by <see cref="OnGetNext(CancellationToken)"/>.
        /// </remarks>
        Task RemoveAsync(StorageQueueMessage message, CancellationToken token);

        /// <summary>
        /// Fetches the number of messages in the queue (or a close approximation).
        /// </summary>
        /// <param name="token">A token to cancel the task with.</param>
        /// <returns>An approximation of the number of messages in the queue or null if no approximation could be made.</returns>
        Task<int?> GetMessageCount(CancellationToken token);
    }

    /// <summary>
    /// Represents a queue to add <see cref="StorageQueueMessage{T}"/>s to and receive <see cref="StorageQueueMessage{T}"/>s from.
    /// </summary>
    public interface IStorageQueue<T>
    {
        /// <summary>
        /// Adds a message containing <paramref name="contents"/> to the queue.
        /// </summary>
        /// <param name="contents">The contents of a message to be added to the queue.</param>
        /// <param name="token">A token to cancel the task with.</param>
        Task AddAsync(T contents, CancellationToken token);

        /// <summary>
        /// Receives a message from the queue.
        /// </summary>
        /// <param name="token">A token to cancel the task with.</param>
        /// <returns>The message from the queue.</returns>
        /// <remarks>
        /// The message is not removed when this method is called and may be returned by subsequent calls to this method.
        /// To remove the message, call <see cref="RemoveAsync(StorageQueueMessage{T}, CancellationToken)"/>.
        /// </remarks>
        Task<StorageQueueMessage<T>> GetNextAsync(CancellationToken token);

        /// <summary>
        /// Removes a message from the queue.
        /// </summary>
        /// <param name="message">The message to be removed from the queue.</param>
        /// <param name="token">A token to cancel the task with.</param>
        /// <remarks>
        /// <paramref name="message"/> MUST have been previously returned by <see cref="GetNextAsync(CancellationToken)"/>.
        /// </remarks>
        Task RemoveAsync(StorageQueueMessage<T> message, CancellationToken token);

        /// <summary>
        /// Fetches the number of messages in the queue (or a close approximation).
        /// </summary>
        /// <param name="token">A token to cancel the task with.</param>
        /// <returns>An approximation of the number of messages in the queue or null if no approximation could be made.</returns>
        Task<int?> GetMessageCount(CancellationToken token);
    }
}