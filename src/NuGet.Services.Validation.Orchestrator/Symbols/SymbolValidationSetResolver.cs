// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Validation.Orchestrator;

namespace NuGet.Services.Validation.Symbols
{
    /// <summary>
    /// Identifies the symbol validation set for a validator request.
    /// </summary>
    internal static class SymbolValidationSetResolver
    {
        public static async Task<ValidatingType> GetValidatingTypeAsync(IValidationStorageService storageService, INuGetValidationRequest request)
        {
            var validationSet = await storageService.TryGetParentValidationSetAsync(request.ValidationId);
            if (validationSet == null || validationSet.PackageKey != request.PackageKey)
            {
                throw new InvalidOperationException($"Cannot find the matching validation set for {request.ValidationId}.");
            }

            if (validationSet.ValidatingType != ValidatingType.SymbolPackage && validationSet.ValidatingType != ValidatingType.StagedSymbolPackage)
            {
                throw new NotSupportedException($"The validating type '{validationSet.ValidatingType}' is not supported by symbol validation.");
            }

            return validationSet.ValidatingType;
        }
    }
}
