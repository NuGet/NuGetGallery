// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Validation.Orchestrator
{
    public abstract class BaseNuGetValidator
    {
        public virtual Task CleanUpAsync(INuGetValidationRequest request)
        {
            return Task.CompletedTask;
        }
    }
}
