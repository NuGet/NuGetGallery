// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using NuGet.Jobs.Validation.Symbols.Core;
using NuGet.Services.Validation;

namespace Validation.Symbols
{
    public interface ISymbolsValidatorService
    {
        /// <summary>
        /// Validates the symbol package.
        /// </summary>
        /// <param name="message">The <see cref="SymbolsValidatorMessage"/> regarding to the symbols package to be validated..</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The validation result.</returns>
        Task<INuGetValidationResponse> ValidateSymbolsAsync(SymbolsValidatorMessage message, CancellationToken token);
    }
}
