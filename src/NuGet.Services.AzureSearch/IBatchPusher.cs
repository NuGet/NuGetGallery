// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// This is a stateful interface that handles pushing index actions to Azure Search and updating the version list
    /// resource based on the enqueued <see cref="IndexActions"/> in a batch-wise fashion. This interface is not
    /// designed to be thread-safe.
    /// </summary>
    public interface IBatchPusher
    {
        /// <summary>
        /// This method does not work with Azure Search or the version lists. It simply enqueues the actions in memory
        /// and associates the work with the provided package ID.
        /// </summary>
        /// <param name="packageId">The package ID related to the index actions.</param>
        /// <param name="indexActions">The index actions and version list.</param>
        void EnqueueIndexActions(string packageId, IndexActions indexActions);

        /// <summary>
        /// Pushes full batches to Azure Search based on the based provided to
        /// <see cref="EnqueueIndexActions(string, IndexActions)"/>. If there is not enough data to create a full batch,
        /// it is not pushed by this method (until enough additional data is enqueued to make a full batch). When all of
        /// the index actions for a specific package ID completed (pushed to Azure Search), the corresponding version
        /// list is also updated. Hijack index changes are applied before search index changes.
        /// </summary>
        /// <exception cref="StorageException">Thrown if the one of the version lists has changed.</exception>
        Task PushFullBatchesAsync();

        /// <summary>
        /// Same as <see cref="PushFullBatchesAsync"/> but if there is a partial batch remaining, it is also pushed.
        /// </summary>
        /// <exception cref="StorageException">Thrown if the one of the version lists has changed.</exception>
        Task FinishAsync();
    }
}