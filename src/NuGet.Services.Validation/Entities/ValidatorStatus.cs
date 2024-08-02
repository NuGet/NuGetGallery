// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Validation
{
    /// <summary>
    /// The Orchestrator integrates with a downstream validation job by implementing a "validator".
    /// The Orchestrator, through its validator, creates this entity when it starts a new validation step
    /// and polls the entity to receive status updates. Meanwhile, the downstream validation job
    /// passes information back to the Orchestrator by updating this entity.
    /// </summary>
    public class ValidatorStatus
    {
        /// <summary>
        /// The unique identifier for this validation step. The Validation Orchestrator generates a unique
        /// validation ID for each validation step it runs on a single package.
        /// </summary>
        public Guid ValidationId { get; set; }

        /// <summary>
        /// The package key in the NuGet gallery database.
        /// </summary>
        public int PackageKey { get; set; }

        /// <summary>
        /// The name of the "validator" for this validation step.
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

        /// <summary>
        /// The entity type to be validated.
        /// </summary>
        public ValidatingType ValidatingType { get; set; }
    }
}
