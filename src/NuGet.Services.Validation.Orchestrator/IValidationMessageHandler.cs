// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Validation.Orchestrator
{
    public interface IValidationMessageHandler<TEntity> : IMessageHandler<PackageValidationMessageData>
        where TEntity : class, IEntity
    {
    }
}
