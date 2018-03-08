// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Validation.Orchestrator
{
    public abstract class BaseValidator
    {
        public virtual Task CleanUpAsync(IValidationRequest request)
        {
            return Task.CompletedTask;
        }
    }
}
