// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation.ScanAndSign
{
    public interface IScanAndSignEnqueuer
    {
        /// <summary>
        /// Enqueues Scan operation. The message delivery is going to be delayed by <see cref="ScanAndSignEnqueuerConfiguration.MessageDelay"/> setting.
        /// </summary>
        /// <param name="validationId">Validation ID for which scan is requested.</param>
        /// <param name="nupkgUrl">Url of the package to scan.</param>
        /// <param name="context">Request context to pass to the actual scan/sign job.</param>
        Task EnqueueScanAsync(
            Guid validationId,
            string nupkgUrl,
            IReadOnlyDictionary<string, string> context);

        /// <summary>
        /// Enqueues Scan operation. The message delivery is going to be delayed by <paramref name="messageDeliveryDelayOverride"/>.
        /// </summary>
        /// <param name="validationId">Validation ID for which scan is requested.</param>
        /// <param name="nupkgUrl">Url of the package to scan.</param>
        /// <param name="messageDeliveryDelayOverride">The message delivery delay.</param>
        /// <param name="context">Request context to pass to the actual scan/sign job.</param>
        Task EnqueueScanAsync(
            Guid validationId,
            string nupkgUrl,
            IReadOnlyDictionary<string, string> context,
            TimeSpan messageDeliveryDelayOverride);

        /// <summary>
        /// Enqueues Scan And Sign operation. The message delivery is going to be delayed by <see cref="ScanAndSignEnqueuerConfiguration.MessageDelay"/> setting.
        /// </summary>
        /// <param name="validationId">Validation ID for which scan and sign is requested.</param>
        /// <param name="nupkgUrl">Url of the package to scan and sign.</param>
        /// <param name="v3ServiceIndexUrl">The service index URL that should be put on the package's repository signature.</param>
        /// <param name="owners">The list of owners that should be put on the package's repository signature.</param>
        /// <param name="context">Request context to pass to the actual scan/sign job.</param>
        Task EnqueueScanAndSignAsync(
            Guid validationId,
            string nupkgUrl,
            string v3ServiceIndexUrl,
            IReadOnlyList<string> owners,
            IReadOnlyDictionary<string, string> context);

        /// <summary>
        /// Enqueues Scan And Sign operation. The message delivery is going to be delayed by <paramref name="messageDeliveryDelayOverride"/>.
        /// </summary>
        /// <param name="validationId">Validation ID for which scan and sign is requested.</param>
        /// <param name="nupkgUrl">Url of the package to scan and sign.</param>
        /// <param name="v3ServiceIndexUrl">The service index URL that should be put on the package's repository signature.</param>
        /// <param name="owners">The list of owners that should be put on the package's repository signature.</param>
        /// <param name="context">Request context to pass to the actual scan/sign job.</param>
        /// <param name="messageDeliveryDelayOverride">The message delivery delay.</param>
        Task EnqueueScanAndSignAsync(
            Guid validationId,
            string nupkgUrl,
            string v3ServiceIndexUrl,
            IReadOnlyList<string> owners,
            IReadOnlyDictionary<string, string> context,
            TimeSpan messageDeliveryDelayOverride);
    }
}