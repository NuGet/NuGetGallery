// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Validation
{
    /// <summary>
    /// The status of an <see cref="IValidator"/>'s validation of a package. This should be used
    /// by each <see cref="IValidator"/> to persist its state.
    /// </summary>
    public class ValidatorStatus
    {
        /// <summary>
        /// The unique identifier for this validation. The Validation Orchestrator generates a unique
        /// validation ID for each <see cref="IValidator"/> it runs on a single package.
        /// </summary>
        public Guid ValidationId { get; set; }

        /// <summary>
        /// The package key in the NuGet gallery database.
        /// </summary>
        public int PackageKey { get; set; }

        /// <summary>
        /// The name of the <see cref="IValidator"/>.
        /// </summary>
        public string ValidatorName { get; set; }

        /// <summary>
        /// The current status for this validator.
        /// </summary>
        public ValidationStatus State { get; set; }

        /// <summary>
        /// The .nupkg URL returned by a processor.
        /// </summary>
        public string NupkgUrl { get; set; }

        /// <summary>
        /// Used for optimistic concurrency when updating the statuses.
        /// </summary>
        public byte[] RowVersion { get; set; }

        /// <summary>
        /// The validation issues found by this validation.
        /// </summary>
        public virtual ICollection<ValidatorIssue> ValidatorIssues { get; set; }
    }
}
