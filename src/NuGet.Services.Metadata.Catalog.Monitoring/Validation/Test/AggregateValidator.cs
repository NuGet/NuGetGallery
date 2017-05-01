// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Runs a set of <see cref="IValidator"/>s.
    /// </summary>
    public abstract class AggregateValidator
    {
        public AggregateValidator(ValidatorFactory factory, ILogger<AggregateValidator> logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _validators = GetValidators(factory);
        }

        /// <summary>
        /// Returns an <see cref="IEnumerable{IValidationTest}"/> representing all <see cref="IValidator"/>s that should be run.
        /// </summary>
        protected abstract IEnumerable<IValidator> GetValidators(ValidatorFactory factory);

        protected ILogger Logger;

        private IEnumerable<IValidator> _validators;

        /// <summary>
        /// Runs validations returned by <see cref="GetValidators(ValidatorFactory)"/>.
        /// </summary>
        /// <param name="package">The <see cref="PackageIdentity"/> to validate.</param>
        /// <returns>A <see cref="AggregateValidationResult"/> which contains the results of the validation.</returns>
        public async Task<AggregateValidationResult> Validate(ValidationContext data)
        {
            return new AggregateValidationResult
            {
                AggregateValidator = this,
                ValidationResults = await Task.WhenAll(_validators.Select(v => v.Validate(data)))
            };
        }
    }
}
