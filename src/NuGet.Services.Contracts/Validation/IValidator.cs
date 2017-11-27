// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Services.Validation
{
    /// <summary>
    /// The interface of an asynchronous validator. This interface is how the validation orchestrator interacts
    /// with the specific validation logic.
    /// </summary>
    public interface IValidator
    {
        /// <summary>
        /// A read-only method to get the status of a validation. If the <see cref="IValidationRequest.ValidationId"/>
        /// has not been seen by the validator before, <see cref="ValidationStatus.NotStarted"/> should be returned.
        /// The implementation can treat any combination of <see cref="IValidationRequest"/> properties as the unique
        /// identity the validation request.
        /// </summary>
        /// <param name="request">The validation request.</param>
        /// <returns>A task returning the current validation status.</returns>
        Task<ValidationStatus> GetStatusAsync(IValidationRequest request);

        /// <summary>
        /// A method that starts the validation on the provided package. If the validation has already started, this
        /// method should simply return the current status as if <see cref="GetStatusAsync(IValidationRequest)"/> was
        /// called. If the validation has not been started yet, the implementation should start the validation and
        /// return the resulting <see cref="ValidationStatus"/>. <see cref="ValidationStatus.NotStarted"/> should not
        /// be returned from this method and, if it is, the caller is free to repeat the method invocation until some
        /// timeout has expired, at which point the validation can be considered <see cref="ValidationStatus.Failed"/>.
        /// </summary>
        /// <param name="request">The validation request.</param>
        /// <returns>
        /// A task returning the current validation status indicating that the validation has been started and possibly
        /// already completed.
        /// </returns>
        Task<ValidationStatus> StartValidationAsync(IValidationRequest request);
    }
}
