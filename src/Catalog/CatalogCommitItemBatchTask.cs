// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    /// <summary>
    /// Represents an asynchrononous task associated with catalog changes for a specific
    /// <see cref="CatalogCommitItemBatch" /> and potentially spanning multiple commits.
    /// </summary>
    public sealed class CatalogCommitItemBatchTask : IEquatable<CatalogCommitItemBatchTask>
    {
        /// <summary>
        /// Initializes a <see cref="CatalogCommitItemBatchTask" /> instance.
        /// </summary>
        /// <param name="batch">A <see cref="CatalogCommitItemBatch" />.</param>
        /// <param name="task">A <see cref="System.Threading.Tasks.Task" /> tracking completion of
        /// <paramref name="batch" /> processing.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="batch" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <see cref="CatalogCommitItemBatch.Key" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="task" /> is <c>null</c>.</exception>
        public CatalogCommitItemBatchTask(CatalogCommitItemBatch batch, Task task)
        {
            if (batch == null)
            {
                throw new ArgumentNullException(nameof(batch));
            }

            if (batch.Key == null)
            {
                throw new ArgumentException(Strings.ArgumentMustNotBeNull, $"{nameof(batch)}.{nameof(batch.Key)}");
            }

            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            Batch = batch;
            Task = task;
        }

        public CatalogCommitItemBatch Batch { get; }
        public Task Task { get; }

        public override int GetHashCode()
        {
            return Batch.Key.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CatalogCommitItemBatchTask);
        }

        public bool Equals(CatalogCommitItemBatchTask other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            return string.Equals(Batch.Key, other.Batch.Key);
        }
    }
}