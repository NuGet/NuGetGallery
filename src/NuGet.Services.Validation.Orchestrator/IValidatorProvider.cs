// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Interface for the class that provides validator instances by their name
    /// </summary>
    public interface IValidatorProvider
    {
        bool IsValidator(string validatorName);
        bool IsProcessor(string validatorName);
        IValidator GetValidator(string validatorName);
    }
}
