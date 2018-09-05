// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Monitoring.Validation.Test.Catalog
{
    /// <summary>
    /// Runs all catalog validations and aggregates their results.
    /// </summary>
    public sealed class CatalogAggregateValidator : IAggregateValidator
    {
        private List<IValidator> _validators;

        public CatalogAggregateValidator(ValidatorFactory factory, bool requireSignature)
        {
            factory = factory ?? throw new ArgumentNullException(nameof(factory));

            _validators = new List<IValidator>();

            if (requireSignature)
            {
                _validators.Add(factory.Create(typeof(PackageHasSignatureValidator)));
            }
        }

        public string Name => GetType().FullName;

        public async Task<AggregateValidationResult> ValidateAsync(ValidationContext context)
        {
            return new AggregateValidationResult(
                this,
                await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context))));
        }
    }
}
