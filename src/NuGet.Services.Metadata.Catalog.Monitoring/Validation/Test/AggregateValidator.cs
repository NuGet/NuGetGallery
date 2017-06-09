// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Abstract class with the shared functionality between all <see cref="IAggregateValidator"/> implementations.
    /// </summary>
    public abstract class AggregateValidator : IAggregateValidator
    {
        /// <summary>
        /// Returns an <see cref="IEnumerable{IValidationTest}"/> representing all <see cref="IValidator"/>s that should be run.
        /// </summary>
        protected abstract IEnumerable<IValidator> GetValidators(ValidatorFactory factory);

        protected ILogger Logger;

        private IEnumerable<IValidator> _validators;

        [JsonProperty("name")]
        public virtual string Name
        {
            get
            {
                return GetType().FullName;
            }
        }

        public AggregateValidator(ValidatorFactory factory, ILogger<AggregateValidator> logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _validators = GetValidators(factory);
        }

        /// <summary>
        /// Runs validations returned by <see cref="GetValidators(ValidatorFactory)"/>.
        /// </summary>
        /// <param name="package">The <see cref="PackageIdentity"/> to validate.</param>
        /// <returns>A <see cref="AggregateValidationResult"/> which contains the results of the validation.</returns>
        public async Task<AggregateValidationResult> ValidateAsync(ValidationContext data)
        {
            return new AggregateValidationResult(
                this, 
                await Task.WhenAll(_validators.Select(v => v.ValidateAsync(data))));
        }
    }
}
