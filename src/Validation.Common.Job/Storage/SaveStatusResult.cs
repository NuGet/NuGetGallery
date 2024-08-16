// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.Validation.Storage
{
    /// <summary>
    /// The possible results for <see cref="IValidatorStateService.SaveStatusAsync(string, ValidatorStatus)"/>.
    /// </summary>
    public enum SaveStatusResult
    {
        /// <summary>
        /// Successfully persisted the updated <see cref="ValidatorStatus"/>
        /// </summary>
        Success,

        /// <summary>
        /// The <see cref="ValidatorStatus"/> is stale. The status should be refetched using
        /// <see cref="IValidatorStateService.GetStatusAsync(string, INuGetValidationRequest)"/> before attempting
        /// to save again.
        /// </summary>
        StaleStatus
    }
}