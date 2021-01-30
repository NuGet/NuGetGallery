// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Validation.PackageSigning.ProcessSignature
{
    /// <summary>
    /// Kicks off package signature verification.
    /// </summary>
    public interface IProcessSignatureEnqueuer
    {
        /// <summary>
        /// Processes the package's signatures, if any. Unacceptable repository signatures will be stripped off.
        /// Author signatures that fail trust or integrity verification will fail the validation.
        /// </summary>
        /// <remarks>
        /// Verification will begin when the <see cref="ValidationEntitiesContext"/> has a <see cref="ValidatorStatus"/>
        /// that matches the <see cref="INuGetValidationRequest"/>'s validationId. Once verification completes,
        /// the <see cref="ValidatorStatus"/>'s State will be updated to "Succeeded" or "Failed".
        /// </remarks>
        /// <param name="request">The request that details the package to be verified.</param>
        /// <param name="requireRepositorySignature">
        /// If true, the package must have an acceptable repository signature to pass validation.
        /// </param>
        /// <returns>A task that will complete when the verification process has been queued.</returns>
        Task EnqueueProcessSignatureAsync(INuGetValidationRequest request, bool requireRepositorySignature);
    }
}
