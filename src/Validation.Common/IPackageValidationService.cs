// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation.Common
{
    /// <summary>
    /// The interface for starting validations. Only the VCS validation is initiated using this interface.
    /// </summary>
    public interface IPackageValidationService
    {
        /// <summary>
        /// Start a validation of the specified package. The validation ID is provided by the caller.
        /// </summary>
        /// <param name="package">The package to validate.</param>
        /// <param name="validators">The well-known names of validators to run.</param>
        /// <param name="validationId">The validation ID to use.</param>
        /// <returns>A task that completes when the validation has started.</returns>
        /// <exception cref="Microsoft.WindowsAzure.Storage.StorageException">Thrown when the validation has already been started.</exception>
        Task StartValidationProcessAsync(NuGetPackage package, string[] validators, Guid validationId);
    }
}