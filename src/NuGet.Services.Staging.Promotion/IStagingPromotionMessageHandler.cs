// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using NuGet.Services.ServiceBus;
using NuGet.Services.Staging;

namespace NuGet.Services.Staging.Promotion
{
    public interface IStagingPromotionMessageHandler<TEntity> : IMessageHandler<StagingPromotionMessage>
        where TEntity : class, IEntity
    {
    }
}
