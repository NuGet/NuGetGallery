// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class AggregateValidationResult
    {
        [JsonProperty("validator")]
        public IValidatorIdentity AggregateValidator { get; }

        [JsonProperty("results")]
        public IEnumerable<ValidationResult> ValidationResults { get; }

        public AggregateValidationResult(
            IValidatorIdentity aggregateValidator, 
            IEnumerable<ValidationResult> validationResults)
        {
            AggregateValidator = aggregateValidator;
            ValidationResults = validationResults;
        }

        [JsonConstructor]
        public AggregateValidationResult(
            ValidatorIdentity validator,
            IEnumerable<ValidationResult> results)
        {
            AggregateValidator = validator;
            ValidationResults = results;
        }
    }
}
