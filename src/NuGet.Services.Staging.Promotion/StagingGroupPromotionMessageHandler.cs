// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGet.Services.Staging;

namespace NuGet.Services.Staging.Promotion
{
    /// <summary>
    /// Provides the handler boundary for staging group promotion commands.
    /// </summary>
    public class StagingGroupPromotionMessageHandler : IStagingPromotionMessageHandler<StagingGroup>
    {
        /// <inheritdoc />
        public Task<bool> HandleAsync(StagingPromotionMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (message.TargetType != StagingPromotionTargetType.StagingGroup)
            {
                throw new ArgumentException("The promotion message must identify a staging group.", nameof(message));
            }

            throw new NotSupportedException("Staging group promotion is not implemented.");
        }
    }
}
