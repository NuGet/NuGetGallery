// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    /// <summary>
    /// A table-as-queue of staging blobs awaiting deletion. Entries are enqueued in the same transaction that removes
    /// a <see cref="StagedPackage"/> row, and drained later by the Gallery.Maintenance cleanup task.
    /// </summary>
    /// <remarks>
    /// Blob deletion is deferred rather than performed inline so that promotion and delete requests do not block on
    /// storage calls. This follows the existing PackageRevalidations table-as-queue pattern.
    /// <para>
    /// The queue is drained in primary key order, which the clustered key already provides, so
    /// <see cref="CreatedDate"/> is deliberately not indexed. It exists for diagnosing a backed-up queue, not for
    /// ordering it.
    /// </para>
    /// </remarks>
    public class StagingBlobCleanup : IEntity
    {
        /// <summary>
        /// Matches <see cref="StagedPackage.MaxBlobPathLength"/>: every path enqueued here comes from a
        /// <see cref="StagedPackage"/>, so the two must not drift.
        /// </summary>
        public const int MaxBlobPathLength = StagedPackage.MaxBlobPathLength;

        /// <summary>
        /// Gets or sets the primary key for the entity.
        /// </summary>
        public int Key { get; set; }

        /// <summary>
        /// Gets or sets the path of the blob in the staging container that needs to be deleted.
        /// </summary>
        [Required]
        [StringLength(MaxBlobPathLength)]
        public string BlobPath { get; set; }

        /// <summary>
        /// Gets or sets when this entry was enqueued. The timestamp is in UTC.
        /// </summary>
        public DateTime CreatedDate { get; set; }
    }
}
