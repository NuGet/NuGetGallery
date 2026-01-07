// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.Validation.Storage
{
    /// <summary>
    /// The possible results for <see cref="IValidatorStateService.AddStatusAsync(string, ValidatorStatus)"/>.
    /// </summary>
    public enum AddStatusResult
    {
        /// <summary>
        /// Successfully persisted the <see cref="ValidatorStatus"/>.
        /// </summary>
        Success,

        /// <summary>
        /// Failed to persist the <see cref="ValidatorStatus"/> as a status already
        /// exists with the same validation id.
        /// </summary>
        StatusAlreadyExists,
    }
}
