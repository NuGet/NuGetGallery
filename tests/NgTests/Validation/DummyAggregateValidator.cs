// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Monitoring;

namespace NgTests
{
    public class DummyAggregateValidator : IAggregateValidator
    {
        private IEnumerable<ValidationResult> _results;

        public string Name => nameof(DummyAggregateValidator);

        public DummyAggregateValidator(IEnumerable<ValidationResult> results)
        {
            _results = results;
        }

        public AggregateValidationResult Validate()
        {
            return new AggregateValidationResult(this, _results);
        }

        public Task<AggregateValidationResult> ValidateAsync(ValidationContext context)
        {
            return Task.FromResult(Validate());
        }
    }
}