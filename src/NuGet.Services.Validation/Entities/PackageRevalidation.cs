// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Validation
{
    /// <summary>
    /// The state for a single package revalidation.
    /// </summary>
    public class PackageRevalidation
    {
        /// <summary>
        /// The database-mastered identifier for this revalidate.
        /// </summary>
        public long Key { get; set; }

        /// <summary>
        /// The package ID. Has a maximum length of 128 unicode characters as defined by the NuGet Gallery database.
        /// </summary>
        public string PackageId { get; set; }

        /// <summary>
        /// The normalized package version. Has a maximum length of 64 unicode characters as defined by the NuGet
        /// Gallery database.
        /// </summary>
        public string PackageNormalizedVersion { get; set; }

        /// <summary>
        /// The time at which the revalidation was enqueued, or null if the revalidation hasn't started yet.
        /// </summary>
        public DateTime? Enqueued { get; set; }

        /// <summary>
        /// The GUID used when enqueueing the revalidation. This will be used by the Orchestrator to generate
        /// a validation set. This cannot be a foreign key constraint because this record is inserted before the
        /// validation set is created.
        /// </summary>
        public Guid? ValidationTrackingId { get; set; }

        /// <summary>
        /// Whether this revalidation has been completed.
        /// </summary>
        public bool Completed { get; set; }

        /// <summary>
        /// Used for optimistic concurrency when updating revalidations.
        /// </summary>
        public byte[] RowVersion { get; set; }
    }
}
