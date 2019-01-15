// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Performs a validation test on a <see cref="ValidationContext"/>.
    /// </summary>
    public interface IValidator : IValidatorIdentity
    {
        /// <summary>
        /// Validates a package.
        /// </summary>
        /// <returns>A <see cref="ValidationResult"/> which contains the results of the validation.</returns>
        Task<ValidationResult> ValidateAsync(ValidationContext context);
    }

    /// <summary>
    /// Performs a validation test on a package on a <see cref="EndpointValidator"/>.
    /// </summary>
    /// <typeparam name="T">The <see cref="EndpointValidator"/> to be validated.</typeparam>
    public interface IValidator<T> : IValidator where T : IEndpoint
    {
    }
}