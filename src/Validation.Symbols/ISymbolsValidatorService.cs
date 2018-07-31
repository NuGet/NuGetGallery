// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Validation;

namespace Validation.Symbols
{
    public interface ISymbolsValidatorService
    {
        /// <summary>
        /// Validates the symbols against the PE files. 
        /// </summary>
        /// <param name="packageId">The package Id.</param>
        /// <param name="packageNormalizedVersion">The package normalized version.</param>
        /// <param name="token">A cancellation token to be used for cancellation of the async execution.</param>
        /// <returns></returns>
        Task<IValidationResult> ValidateSymbolsAsync(string packageId, string packageNormalizedVersion, CancellationToken token);
    }
}
