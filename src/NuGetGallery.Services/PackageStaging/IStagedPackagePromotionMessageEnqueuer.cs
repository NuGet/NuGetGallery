// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Staging;

namespace NuGetGallery
{
    /// <summary>
    /// Enqueues staged package promotion messages.
    /// </summary>
    public interface IStagedPackagePromotionMessageEnqueuer
    {
        /// <summary>
        /// Enqueues a staged package promotion message.
        /// </summary>
        /// <param name="message">The staged package promotion message.</param>
        Task SendMessageAsync(StagedPackagePromotionMessage message);
    }
}
