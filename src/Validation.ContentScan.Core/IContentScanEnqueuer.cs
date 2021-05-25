// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation.ContentScan
{
    public interface IContentScanEnqueuer
    {
        /// <summary>
        /// Enqueues Scan operation. The message delivery is going to be delayed by <see cref="ScanAndSignEnqueuerConfiguration.MessageDelay"/> setting.
        /// </summary>
        /// <param name="validationStepId">Validation ID for which scan is requested.</param>
        /// <param name="contentUri">Url of the package to scan.</param>
        Task EnqueueContentScanAsync(Guid validationStepId, Uri contentUri);

        /// <summary>
        /// Enqueues Scan operation. The message delivery is going to be delayed by <paramref name="messageDeliveryDelayOverride"/>.
        /// </summary>
        /// <param name="validationStepId">Validation ID for which scan is requested.</param>
        /// <param name="contentUri">Url of the package to scan.</param>
        /// <param name="messageDeliveryDelayOverride">The message delivery delay.</param>
        Task EnqueueContentScanAsync(Guid validationStepId, Uri contentUri, TimeSpan messageDeliveryDelayOverride);
    }
}