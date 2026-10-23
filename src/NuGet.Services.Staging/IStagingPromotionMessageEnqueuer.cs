// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Staging
{
    /// <summary>
    /// Enqueues staging promotion messages.
    /// </summary>
    public interface IStagingPromotionMessageEnqueuer
    {
        /// <summary>
        /// Enqueues a staging promotion message.
        /// </summary>
        /// <param name="message">The staging promotion message.</param>
        Task SendMessageAsync(StagingPromotionMessage message);
    }
}
