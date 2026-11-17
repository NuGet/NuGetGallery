// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Completes staged symbol validation immediately when asynchronous validation is disabled.
    /// </summary>
    public class ImmediateStagedSymbolPackageValidationMessageEmitter : IStagedSymbolPackageValidationMessageEmitter
    {
        public Task<StagedPackageStatus> StartValidationAsync(StagedSymbolPackage stagedSymbolPackage)
        {
            if (stagedSymbolPackage == null)
            {
                throw new ArgumentNullException(nameof(stagedSymbolPackage));
            }

            return Task.FromResult(StagedPackageStatus.Ready);
        }
    }
}
