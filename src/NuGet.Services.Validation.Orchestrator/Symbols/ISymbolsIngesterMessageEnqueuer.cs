// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Jobs.Validation.Symbols.Core;

namespace NuGet.Services.Validation.Symbols
{
    public interface ISymbolsIngesterMessageEnqueuer
    {
        /// <summary>
        /// Enqueues a message to one of the topics used by the Symbol Ingester.
        /// </summary>
        /// <param name="request">The validation request.</param>
        /// <returns>A <see cref="Task"/> that will be completed when the execution is completed.</returns>
        Task<SymbolsIngesterMessage> EnqueueSymbolsIngestionMessageAsync(INuGetValidationRequest request);
    }
}
