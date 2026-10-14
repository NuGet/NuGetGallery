// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Suppresses publication and owner notifications during private symbol validation.
    /// </summary>
    public class StagedSymbolPackageMessageService : IMessageService<StagedSymbolPackage>
    {
        public Task SendPublishedMessageAsync(StagedSymbolPackage entity)
        {
            // TODO: Send a staging-specific ready email.
            return Task.CompletedTask;
        }

        public Task SendValidationFailedMessageAsync(StagedSymbolPackage entity, PackageValidationSet validationSet)
        {
            // TODO: Send a staging-specific validation failed email.
            return Task.CompletedTask;
        }

        public Task SendValidationTakingTooLongMessageAsync(StagedSymbolPackage entity)
        {
            // TODO: Send a staging-specific validation taking too long email.
            return Task.CompletedTask;
        }
    }
}
