// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Validation
{
    /// <summary>
    /// The interface of an asynchronous validator. This interface is how the validation orchestrator interacts
    /// with the specific validation logic. A validator can either be read-only or read-write (i.e. a "processor"). If
    /// the <see cref="IValidationResult.NupkgUrl"/> is non-null, then the validator has modified that contents
    /// (.nupkg) of the package, meaning the validator is read-write. If the <see cref="IValidationResult.NupkgUrl"/>
    /// is null, then the package content has not been modified ans the validator is read-only. Note that is it
    /// possible for a read-write validator to return a null <see cref="NupkgUrl"/>  indicating that it chose not to
    /// modify the package content (e.g. no-op). If a validator is read-write, it should implement
    /// <see cref="IProcessor"/> and is referred to as a "processor".
    /// </summary>
    public interface IValidator
    {
        /// <summary>
        /// A read-only method to get the result of a validation. If the <see cref="IValidationRequest.ValidationId"/>
        /// has not been seen by the validator before, a result with a status of <see cref="ValidationStatus.NotStarted"/>
        /// should be returned. The implementation can treat any combination of <see cref="IValidationRequest"/> properties
        /// as the unique identity the validation request.
        /// </summary>
        /// <param name="request">The validation request.</param>
        /// <returns>A task returning the current validation result.</returns>
        Task<IValidationResult> GetResultAsync(IValidationRequest request);

        /// <summary>
        /// A method that starts the validation on the provided package. If the validation has already started, this
        /// method should simply return the current status as if <see cref="GetResultAsync(IValidationRequest)"/> was
        /// called. If the validation has not been started yet, the implementation should start the validation and
        /// return the resulting <see cref="IValidationResult"/>. A result with a status of <see cref="ValidationStatus.NotStarted"/>
        /// should not be returned from this method and, if it is, the caller is free to repeat the method invocation until some
        /// timeout has expired, at which point the validation can be considered <see cref="ValidationStatus.Failed"/>.
        /// </summary>
        /// <param name="request">The validation request.</param>
        /// <returns>
        /// A task returning the current validation result indicating that the validation has been started and possibly
        /// already completed.
        /// </returns>
        Task<IValidationResult> StartAsync(IValidationRequest request);

        /// <summary>
        /// A method that can be used to clean up any state produced by <see cref="StartAsync(IValidationRequest)"/>.
        /// If this method fails for some reason, an exception should be thrown. However, the caller can simply swallow
        /// the exception making this method a best effort. This is simply a way, in the happy path, to keep storage
        /// usage under control.
        /// </summary>
        /// <param name="request">The validation request.</param>
        Task CleanUpAsync(IValidationRequest request);
    }
}
