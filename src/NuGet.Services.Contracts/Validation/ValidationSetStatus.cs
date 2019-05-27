// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation
{
    /// <summary>
    /// The status of the validation set.
    /// </summary>
    public enum ValidationSetStatus
    {
        /// <summary>
        /// The validation set has an unknown status. Validation sets started and not completed before the introduction
        /// of the status field have an unknown status.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The validation set is in progress. This indicates that there is one or more validations that have not
        /// completed yet or the orchestrator timed out while checking validation statuses. When validation sets are
        /// first created, they have this status.
        /// </summary>
        InProgress = 1,

        /// <summary>
        /// The validation set has completed. This status can occur either when one of validations has failed or when
        /// all of the validations have succeeded. In other words, it represents both the "failed" and "succeed"
        /// outcomes.
        /// </summary>
        Completed = 2,
    }
}