// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Validation.Symbols
{
    public interface ISymbolsMessageEnqueuer
    {
        /// <summary>
        /// Enqueues a message to one of the topics used by the Symbol validators
        /// </summary>
        /// <param name="request">The validtion request.</param>
        /// <returns>A <see cref="Task"/> that will be completed when the execution is completed.</returns>
        Task EnqueueSymbolsValidationMessageAsync(INuGetValidationRequest request);
    }
}
