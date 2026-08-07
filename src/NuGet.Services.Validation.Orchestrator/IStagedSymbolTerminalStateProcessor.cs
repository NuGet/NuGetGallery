// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGet.Services.Validation.Orchestrator
{
    public interface IStagedSymbolTerminalStateProcessor
    {
        Task ProcessAsync(PackageValidationSet validationSet, SymbolPackage symbolPackage, StagingArtifactStatus status);
    }
}
