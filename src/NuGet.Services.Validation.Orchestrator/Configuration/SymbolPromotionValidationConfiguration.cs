// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Jobs.Validation;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Creates ingestion-only settings without changing ordinary symbol validation prerequisites.
    /// </summary>
    public static class SymbolPromotionValidationConfiguration
    {
        public static bool IsPromotion(PackageValidationSet validationSet)
        {
            if (validationSet == null)
            {
                throw new ArgumentNullException(nameof(validationSet));
            }

            return validationSet.ValidatingType == ValidatingType.StagedSymbolPackage
                && validationSet.PackageValidations.Count == 1
                && validationSet.PackageValidations.Single().Type == ValidatorName.SymbolsIngester;
        }

        public static ValidationConfigurationItem Create(ValidationConfiguration symbolsConfiguration)
        {
            if (symbolsConfiguration == null)
            {
                throw new ArgumentNullException(nameof(symbolsConfiguration));
            }

            if (!symbolsConfiguration.EnableStagedSymbolPromotion)
            {
                throw new NotSupportedException("Staged symbol promotion is not enabled.");
            }

            var ingester = symbolsConfiguration.Validations?.SingleOrDefault(validation => validation.Name == ValidatorName.SymbolsIngester);
            if (ingester == null)
            {
                throw new ArgumentException("Symbol promotion requires SymbolsIngester configuration.", nameof(symbolsConfiguration));
            }

            return new ValidationConfigurationItem
            {
                Name = ValidatorName.SymbolsIngester,
                ShouldStart = true,
                FailureBehavior = ValidationFailureBehavior.MustSucceed,
                TrackAfter = ingester.TrackAfter,
            };
        }
    }
}
