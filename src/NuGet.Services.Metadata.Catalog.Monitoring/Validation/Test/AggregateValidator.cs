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
        protected readonly IEnumerable<IValidator> Validators;

        protected readonly ILogger<AggregateValidator> Logger;

        [JsonProperty("name")]
        public virtual string Name
        {
            get
            {
                return GetType().FullName;
            }
        }

        public AggregateValidator(IEnumerable<IValidator> validators, ILogger<AggregateValidator> logger)
        {
            Validators = validators ?? throw new ArgumentNullException(nameof(validators));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Runs validations returned by <see cref="GetValidators(ValidatorFactory)"/>.
        /// </summary>
        /// <param name="package">The <see cref="PackageIdentity"/> to validate.</param>
        /// <returns>A <see cref="AggregateValidationResult"/> which contains the results of the validation.</returns>
        public async Task<AggregateValidationResult> ValidateAsync(ValidationContext context)
        {
            return new AggregateValidationResult(
                this, 
                await Task.WhenAll(Validators.Select(v => v.ValidateAsync(context))));
        }
    }
}