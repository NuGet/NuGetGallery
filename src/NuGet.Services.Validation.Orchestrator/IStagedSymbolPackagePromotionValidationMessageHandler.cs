// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.ServiceBus;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Processes ingestion and completion messages for accepted staged symbol promotions.
    /// </summary>
    public interface IStagedSymbolPackagePromotionValidationMessageHandler : IMessageHandler<PackageValidationMessageData>
    {
    }
}
