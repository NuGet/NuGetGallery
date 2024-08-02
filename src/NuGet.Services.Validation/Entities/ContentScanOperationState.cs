// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Validation.Entities
{
    /// <summary>
    /// Status of the cvs scan operations
    /// </summary>
    public class ContentScanOperationState
    {
        /// <summary>
        /// The database-mastered identifier for this operation.
        /// </summary>
        public long Key { get; set; }

        /// <summary>
        /// Validation Step ID for which this operation is performed.
        /// </summary>
        public Guid ValidationStepId { get; set; }

        /// <summary>
        /// Status of the operation performed.
        /// </summary>
        public ContentScanOperationStatus Status { get; set; }

        /// <summary>
        /// CVS's ID for the content scan job.
        /// </summary>
        public string JobId { get; set; }

        /// <summary>
        /// Type of the Scan performed.
        /// </summary>
        public ContentScanType Type { get; set; }

        /// <summary>
        /// Time when the validator detected operation request.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Time when the validator last polled for result.
        /// </summary>
        public DateTime PolledAt { get; set; }

        /// <summary>
        /// Time when the operation updated.
        /// </summary>
        public DateTime? FinishedAt { get; set; }

        /// <summary>
        /// The path pointing to file inside extension
        /// </summary>
        public string ContentPath { get; set; }

        /// <summary>
        /// File ID to correlate with CVS response
        /// </summary>
        public string FileId { get; set; }

        /// <summary>
        /// Used for optimistic concurrency when updating statuses.
        /// </summary>
        public byte[] RowVersion { get; set; }
    }
}