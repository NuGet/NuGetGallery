// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Runs a set of <see cref="IValidator"/>s.
    /// </summary>
    public interface IAggregateValidator : IValidatorIdentity
    {
        /// <summary>
        /// Runs each <see cref="IValidator"/> and returns an <see cref="AggregateValidationResult"/> containing all results.
        /// </summary>
        Task<AggregateValidationResult> ValidateAsync(ValidationContext context);
    }
}