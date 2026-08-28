// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class ImmediateStagedPackageValidationMessageEmitter : IStagedPackageValidationMessageEmitter
    {
        public Task<bool> StartValidationAsync(StagedPackage stagedPackage)
        {
            return Task.FromResult(false);
        }
    }
}
